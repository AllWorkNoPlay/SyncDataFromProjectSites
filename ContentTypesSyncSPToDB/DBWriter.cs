using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ContentTypesSyncSPToDB.Model;

namespace ContentTypesSyncSPToDB
{
    public class DBWriter
    {
        public IEnumerable<string> GenerateTableCreationScript(IList<Item> items)
        {
            var tables = items.Select(x => x.DBTableName).Distinct();

            foreach (var table in tables)
            {
                var columns = items
                                .Where(x => x.DBTableName.Equals(table))
                                .SelectMany(x => x.Properties)
                                .Select(x => new { Name = x.Name, Type = x.Type })
                                .Distinct();
                


                var ddlColumns = string.Join(",", columns.Select(c => $"[{c.Name}] {GetType(c.Type)}"));
                yield return $"create table [{table}] ({ddlColumns}); ";
            }
        }

        public IEnumerable<string> GenerateDropTableScript(IList<Item> items)
        {
            var tables = items.Select(x => x.DBTableName).Distinct();

            foreach (var table in tables)
            {
                //yield return $"drop table [{table}]; ";
                yield return $"if  exists (select * from sys.objects where object_id = object_id(N'[dbo].[{table}]') and type in (N'U')) drop table[dbo].[{table}]";
            }
        }

        public IEnumerable<string> GenerateInsertScript(IList<Item> items)
        {
            foreach (var item in items)
            {
                var table = item.DBTableName;

                var allColumns = items
                                .Where(x => x.DBTableName.Equals(table))
                                .SelectMany(x => x.Properties)
                                .Select(x => new { Name = x.Name, Type = x.Type })
                                .Distinct();

                var columnNamesString = string.Join(", ", allColumns.Select(c => $"[{c.Name}]"));
                var values = allColumns.Select(c =>
                {
                    var value = item.Properties.Where((p => p.Name.Equals(c.Name))).Select(p=> GetValueString(p)).SingleOrDefault();

                    if (value == null)
                    {
                        switch (c.Type)
                        {
                            case Property.TypeEnum.Number:
                            case Property.TypeEnum.Currency:
                                value ="0";
                                break;
                            default:
                                value = ""; ;
                                break;
                        }
                    }
                    return value;
                }) ;
                var valuesString = string.Join(", ", values.Select(v=>$"'{v}'"));

  

                yield return $"insert into [{table}] ({columnNamesString}) values ({valuesString})";
            }
        }

        public string GetType(Property.TypeEnum type)
        {
            switch (type)
            {
                case Property.TypeEnum.Currency:
                    return "decimal(18,2)";
                case Property.TypeEnum.Number:
                    return "int";
                case Property.TypeEnum.DateTime:
                    return "datetime";
                default:
                    return "nvarchar(max)";
            }
        }



        public void ExecuteScript(IList<string> script, string connectionString)
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction("Sync"))
                {
                    string currentline = String.Empty;

                    try
                    {
                        foreach (var line in script)
                        {
                            currentline = line;
                            var command = new SqlCommand(line, conn, transaction);
                            command.ExecuteNonQuery();
                        }
                        transaction.Commit();
                        conn.Close();
                    }
                    catch (SqlException e)
                    {
                        Logging.Logger.Error(e,
                            $"Error in command: '{currentline}'. Rolling back transaction, no changes made.");

                        transaction.Rollback();
                    }
                }

                conn.Close();
            }
        }

        private object GetValueString(Property p)
        {
            switch (p.Type)
            {

                case Property.TypeEnum.Number:
                    return p.Value;

                //case Property.TypeEnum.DateTime:
                default:
                    return $"{p.Value.Replace("'","''")}";
            }
        }

    }
}
