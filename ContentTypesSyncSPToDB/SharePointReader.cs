using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ContentTypesSyncSPToDB.Model;
using Microsoft.ProjectServer.Client;
using Microsoft.SharePoint.Client;
using Serilog.Context;
using Serilog.Core;

namespace ContentTypesSyncSPToDB
{
    public class SharePointReader
    {
        static CultureInfo CultureInfoIE = new CultureInfo("en-IE");
        static public List<Item> Read(Configuration.Configuration configuration)
        {
            var endresult = new List<Item>();
            

            var activeProjects = GetActiveProjects(configuration.PWASiteCollectionURL).ToList();
            activeProjects.Add(new {Url= configuration.PWASiteCollectionURL, Guid=""});

            Parallel.ForEach(
                activeProjects, 
                new ParallelOptions { MaxDegreeOfParallelism = configuration.MaxDegreeOfParallelism ?? 5 }, 
                (project) => endresult.AddRange(new SharePointReader().GetItems(configuration, project.Url, project.Guid)));
            

            return endresult;
        }

        private IEnumerable<Item> GetItems(Configuration.Configuration configuration, string url, string guid)
        {
            using (ClientContext ctx = new ClientContext(url))
            {
                Web web = ctx.Web;
                ctx.Load(web, website => website.Title, website => website.Url);
                ctx.ExecuteQuery();

                using (LogContext.PushProperty("Web", web.Title))
                {
                    var lists = web.Lists;

                    ctx.Load(lists, x => x.Include(y => y.Title, y => y.ParentWebUrl, y => y.Fields));
                    ctx.ExecuteQuery();

                    foreach (var configlist in configuration.Lists)
                    {
                        using (LogContext.PushProperty("List", $"{configlist.SPName} - {configlist.ContentType}"))
                        {
                            var list =
                                lists.FirstOrDefault(
                                    l => l.Title.Equals(configlist.SPName, StringComparison.CurrentCultureIgnoreCase));
                            if (list == null)
                            {
                                var sb = new StringBuilder();

                                foreach (var l in lists)
                                {
                                    if (sb.Length > 0)
                                        sb.Append(", ");
                                    sb.Append($"'{l.Title}'");
                                }
                                Logging.Logger.Warning(
                                    $"Cannot find list '{configlist.SPName}' in web '{web.Title}', lists are {sb}");
                                continue;
                            }

                            var contentTypes = list.ContentTypes;
                            ctx.Load(contentTypes);
                            ctx.ExecuteQuery();

                            FieldCollection fields = null;



                            if (configlist.ContentType != null)
                            {
                                var contentType =
                                    contentTypes.SingleOrDefault(ct => ct.Name.Equals(configlist.ContentType));
                                if (contentType != null)
                                {
                                    fields = contentType.Fields;
                                }
                                else
                                {
                                    continue;
                                }
                            }
                            else
                            {
                                fields = list.Fields;
                            }

                            ctx.Load(fields, x => x.Include(
                                y => y.Title,
                                y => y.InternalName,
                                y => y.TypeAsString,
                                y => y.FromBaseType));
                            ctx.ExecuteQuery();

                            Dictionary<string, dynamic> columnNamesDict = new Dictionary<string, object>();
                            foreach (var field in fields)
                            {
                                if (!field.FromBaseType || (field.InternalName.Equals("Title") && (field.Title!="Title")))
                                {
                                    columnNamesDict[field.Title] =
                                        new {InternalName = field.InternalName, Type = field.TypeAsString};
                                }
                            }

                            var fieldRefs = string.Join("",
                                columnNamesDict.Values.Select(c => $"<FieldRef Name='{c.InternalName}' />"));

                            var r = list.GetItems(
                                new CamlQuery()
                                {
                                    ViewXml =
                                        $"<View><Query><OrderBy> FieldRef Name = 'ID'/></OrderBy></Query><ViewFields>{fieldRefs}</ViewFields></View>"
                                });

                            ctx.Load(r);
                            ctx.ExecuteQuery();

                            if (r.Count > 0)
                            {
                                using (LogContext.PushProperty("List", list.Title))
                                {
                                    Logging.Logger.Debug($"{r.Count} items found");

                                    foreach (var listItem in r)
                                    {
                                        var properties = new List<Property>
                                        {
                                            PropertyFor("Project Name", Property.TypeEnum.String, web.Title),
                                            PropertyFor("Synced", Property.TypeEnum.DateTime,
                                                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")),
                                        };


                                        foreach (var colName in columnNamesDict.Keys)
                                        {
                                            if (listItem.FieldValues.ContainsKey(columnNamesDict[colName].InternalName))
                                            {
                                                var obj = listItem.FieldValues[columnNamesDict[colName].InternalName];
                                                if (obj != null)
                                                {
                                                    var type = columnNamesDict[colName].Type;
                                                    var value = obj as FieldLookupValue;
                                                    if (value != null)
                                                    {
                                                        properties.Add(PropertyFor(colName, MapType(type),
                                                            value.LookupValue));
                                                        properties.Add(PropertyFor($"{colName}_id",
                                                            Property.TypeEnum.Number,
                                                            value.LookupId));
                                                    }
                                                    else
                                                    {
                                                        properties.Add(PropertyFor(colName, MapType(type), obj));
                                                    }
                                                }
                                            }
                                        }

                                        properties.AddRange(new List<Property>
                                        {
                                            PropertyFor("SP_Id", Property.TypeEnum.Number, listItem.Id),
                                            PropertyFor("SP_List", Property.TypeEnum.String, configlist.SPName),
                                            PropertyFor("SP_ContentType", Property.TypeEnum.String,
                                                configlist.ContentType ?? "None"),
                                            PropertyFor("Project Guid", Property.TypeEnum.String, guid)
                                        });

                                        yield return
                                            new Item()
                                            {
                                                ContentType = configlist.ContentType,
                                                DBTableName = configlist.DBTableName,
                                                Properties = properties
                                            };
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private static Property PropertyFor(string colName, Property.TypeEnum type, object value)
        {
            return new Property() { Type = type, Name = colName, Value = value?.ToString() ?? "" };
        }


        private static Property.TypeEnum MapType(string type)
        {
            switch (type)
            {
                case "Currency":
                    return Property.TypeEnum.Currency;
                default:
                    return Property.TypeEnum.String;
            }
        }

        static public IEnumerable<dynamic> GetActiveProjects(string pwaUrl)
        {
            var projContext = new ProjectContext(pwaUrl);

            projContext.Load(projContext.Projects, items => items.IncludeWithDefaultProperties(item => item.ProjectSiteUrl, item=>item.Id));

            projContext.ExecuteQuery();

            foreach (PublishedProject pubProj in projContext.Projects)
            {
                yield return new {Url=pubProj.ProjectSiteUrl, Guid=pubProj.Id.ToString()};
            }
        }
    }
}
