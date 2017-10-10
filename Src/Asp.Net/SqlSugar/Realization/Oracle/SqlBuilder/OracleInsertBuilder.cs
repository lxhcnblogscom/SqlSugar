﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlSugar
{
    public class OracleInsertBuilder : InsertBuilder
    {
        public override string SqlTemplate
        {
            get
            {
                return @"INSERT INTO {0} 
           ({1})
     VALUES
           ({2}) ";

            }
        }
        public override string SqlTemplateBatch
        {
            get
            {
                return "INSERT INTO {0} ({1})";
            }
        }
        public override string ToSqlString()
        {
            var identities = this.EntityInfo.Columns.Where(it => it.OracleSequenceName.HasValue()).ToList();
            if (IsNoInsertNull)
            {
                DbColumnInfoList = DbColumnInfoList.Where(it => it.Value != null).ToList();
            }
            var groupList = DbColumnInfoList.GroupBy(it => it.TableId).ToList();
            var isSingle = groupList.Count() == 1;
            string columnsString = string.Join(",", groupList.First().Select(it => Builder.GetTranslationColumnName(it.DbColumnName)));
            if (isSingle)
            {
                string columnParametersString = string.Join(",", this.DbColumnInfoList.Select(it => Builder.SqlParameterKeyWord + it.DbColumnName));
                if (identities.HasValue())
                {
                    columnsString = columnsString.TrimEnd(',') + "," + string.Join(",", identities.Select(it => Builder.GetTranslationColumnName(it.DbColumnName)));
                    columnParametersString = columnParametersString.TrimEnd(',') + "," + string.Join(",", identities.Select(it => it.OracleSequenceName + ".nextval"));
                }
                return string.Format(SqlTemplate, GetTableNameString, columnsString, columnParametersString);
            }
            else
            {
                StringBuilder batchInsetrSql = new StringBuilder();
                int pageSize = 200;
                int pageIndex = 1;
                int totalRecord = groupList.Count;
                int pageCount = (totalRecord + pageSize - 1) / pageSize;
                if (identities.HasValue())
                {
                    columnsString = columnsString.TrimEnd(',') + "," + string.Join(",", identities.Select(it => Builder.GetTranslationColumnName(it.DbColumnName)));
                }
                while (pageCount >= pageIndex)
                {
                    batchInsetrSql.AppendFormat(SqlTemplateBatch, GetTableNameString, columnsString);
                    int i = 0;
                    foreach (var columns in groupList.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList())
                    {
                        var isFirst = i == 0;
                        if (!isFirst)
                        {
                            batchInsetrSql.Append(SqlTemplateBatchUnion);
                        }
                        var insertColumns = string.Join(",", columns.Select(it => string.Format(SqlTemplateBatchSelect, FormatValue(it.Value), Builder.GetTranslationColumnName(it.DbColumnName))));
                        if (identities.HasValue())
                        {
                            insertColumns = insertColumns.TrimEnd(',') + "," + string.Join(",", identities.Select(it =>
                            {
                                var seqValue = this.OracleSeqInfoList[it.OracleSequenceName];
                                this.OracleSeqInfoList[it.OracleSequenceName] = this.OracleSeqInfoList[it.OracleSequenceName] + 1;
                                return seqValue + 1+" AS "+it.DbColumnName;
                            }));
                        }
                        batchInsetrSql.Append("\r\n SELECT " + insertColumns + " FROM DUAL ");
                        ++i;
                    }
                    pageIndex++;
                    batchInsetrSql.Append("\r\n;\r\n");
                }
                return "BEGIN\r\n"+ batchInsetrSql.ToString()+"\r\nEND;";
            }
        }
    }
}
