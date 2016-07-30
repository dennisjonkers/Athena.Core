using System;
using System.Collections.Generic;
using System.Data;
using System.Web;


namespace Athena.Core
{
    public enum Operators
    {
        Equals,
        GreaterThan,
        GreaterThanEqual,
        LessThan,
        LessThanEqual,
        StartsWith,
        Null,
        NotEqual,
        RegularExpression,
        IsIn,
        NotIsIn,
        Contains

    }

    public class OrBuilder
    {
        public Data DataClass;

        private string _Where = "";

        public override string ToString()
        {
            return _Where;
        }

        private string GetOpSign(Operators op)
        {
            switch (op)
            {
                case Operators.Equals:
                    return " = ";
                case Operators.NotEqual:
                    return " <> ";
                case Operators.GreaterThan:
                    return " > ";
                case Operators.GreaterThanEqual:
                    return " >= ";
                case Operators.LessThan:
                    return " < ";
                case Operators.LessThanEqual:
                    return " <= ";
                case Operators.StartsWith:
                    return " LIKE ";
                case Operators.Contains:
                    return " LIKE ";
                case Operators.RegularExpression:
                    return " REGEXP ";
                case Operators.IsIn:
                    return " IN ";
                case Operators.NotIsIn:
                    return " Not IN ";
                default:
                    return " = ";
            }
        }

        public void AppendWhere(string Statement)
        {
            if (string.IsNullOrEmpty(Statement))
            {
                return;
            }
            if (_Where.Length > 0)
            {
                _Where += " OR ";
            }
            _Where += Statement;

        }

        public void AppendWhere(string Field, Operators Op, DateTime Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " OR ";
            }
            if (Op == Operators.Null)
            {
                _Where += Field;
                _Where += " IS NULL";
            }
            else if (Op == Operators.IsIn | Op == Operators.NotIsIn)
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += "(";
                _Where += DateToSQLString(Value);
                _Where += ")";
            }
            else
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += DateToSQLString(Value);
            }
        }

        public void AppendWhere(string Field, Operators Op, string Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " OR ";
            }
            if (Op == Operators.Null)
            {
                if (!bool.Parse(Value))
                {
                    _Where += "NOT ";
                }
                _Where += Field;
                _Where += " IS NULL";
            }
            else if (Op == Operators.IsIn | Op == Operators.NotIsIn)
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += "(";
                _Where += Value;
                _Where += ")";
            }
            else
            {

                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += "'";
                if (Op == Operators.Contains)
                {
                    _Where += "%";
                }
                _Where += Value.Replace("'", "''");
                if (Op == Operators.StartsWith || Op == Operators.Contains)
                {
                    _Where += "%";
                }

                _Where += "'";
            }
        }

        public void AppendWhere(string Field, Operators Op, decimal Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " OR ";
            }
            _Where += Field;
            _Where += GetOpSign(Op);
            _Where += Value.ToString().Replace(",", ".");
        }

        public void AppendWhere(string Field, Operators Op, long Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " OR ";
            }
            _Where += Field;
            _Where += GetOpSign(Op);
            _Where += Value.ToString();
        }

        public void AppendWhere(string Field, DateTime Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " OR ";
            }
            _Where += Field + " = ";

            _Where += DateToSQLString(Value);

        }

        public string DateToSQLString(DateTime Value)
        {
            if (DataClass == null)
                DataClass = new Data();

            switch (DataClass.Driver)
            {
                case QueryType.MSSQL:
                    return "{d '" + Value.Year + "-" + Value.Month.ToString().PadLeft(2, '0') + "-" + Value.Day.ToString().PadLeft(2, '0') + "'}";
                case QueryType.MYSQL:
                    return "'" + Value.Year + "-" + Value.Month.ToString().PadLeft(2, '0') + "-" + Value.Day.ToString().PadLeft(2, '0') + "'";
                case QueryType.Oracle:
                    return "to_date('" + Value.Year + "-" + Value.Month.ToString().PadLeft(2, '0') + "-" + Value.Day.ToString().PadLeft(2, '0') + "','dd-mm-yyyy'}";
                default:
                    return "";
            }
        }


        public void AppendWhere(string Field, string Value)
        {
            AppendWhere(Field, Operators.Equals, Value);
        }

        public void AppendWhere(string Field, long Value)
        {
            AppendWhere(Field, Operators.Equals, Value);
        }
    }

    /// <summary>
    /// Summary description for QueryBuilder.
    /// </summary>
    public class QueryBuilder : ICloneable
    {
        public Data DataClass;

        public int _Top = 0;
        public int _Start = 0;
        public bool _Distinct = false;
        public string _Select = "";
        public string _Columns = "";
        public string _From = "";
        public string _Where = "";
        public string _OrderBy = "";
        public string _GroupBy = "";
        public string _Having = "";

        #region "Distinct"
        public bool Distinct
        {
            get { return _Distinct; }
            set { _Distinct = value; }
        }
        #endregion

        #region "Top"
        public int Top
        {
            get { return _Top; }
            set { _Top = value; }
        }
        #endregion

        #region "Start"
        public int Start
        {
            get { return _Start; }
            set { _Start = value; }
        }
        #endregion
        #region "Select"
        public void AppendSelect(string Fields)
        {
            if (_Select.Length == 0)
            {
                _Select = Fields;
            }
            else
            {
                _Select += "," + Fields;
            }
        }

        #endregion

        #region "ColumnNames"
        public void AppendColumnNames(string Fields)
        {
            if (_Select.Length == 0)
            {
                _Columns = Fields;
            }
            else
            {
                _Columns += "," + Fields;
            }
        }
        #endregion
        #region "From"
        public enum JoinType
        {
            InnerJoin,
            LeftJoin
        }

        public void AppendFrom(JoinType Type, string Table, string Clause)
        {
            if (_From.Length > 0)
                _From += " ";
            switch (Type)
            {
                case JoinType.InnerJoin:
                    _From += "INNER JOIN ";
                    break;
                case JoinType.LeftJoin:
                    _From += "LEFT OUTER JOIN ";
                    break;
            }
            _From += Table;
            _From += " ON ";
            _From += Clause;
        }

        public void AppendFrom(string Table)
        {
            if (_From.Length > 0)
            {
                _From = Table + " " + _From;
            }
            else
            {
                _From = Table;
            }
        }

        public void AppendFrom(QueryBuilder queryBuilder, string Alias)
        {
            if (_From.Length > 0)
            {
                _From = "(" + queryBuilder.ToString() + ")" + Alias + " " + _From;
            }
            else
            {
                _From = "(" + queryBuilder.ToString() + ")" + Alias;
            }
        }

        #endregion

        #region "Where"

        public enum Operators
        {
            Equals,
            GreaterThan,
            GreaterThanEqual,
            LessThan,
            LessThanEqual,
            StartsWith,
            Null,
            NotEqual,
            RegularExpression,
            IsIn,
            NotIsIn,
            Contains,
            EndsWith
        }

        private string GetOpSign(Operators op)
        {
            switch (op)
            {
                case Operators.Equals:
                    return " = ";
                case Operators.NotEqual:
                    return " <> ";
                case Operators.GreaterThan:
                    return " > ";
                case Operators.GreaterThanEqual:
                    return " >= ";
                case Operators.LessThan:
                    return " < ";
                case Operators.LessThanEqual:
                    return " <= ";
                case Operators.StartsWith:
                    return " LIKE ";
                case Operators.EndsWith:
                    return " LIKE ";
                case Operators.Contains:
                    return " LIKE ";
                case Operators.RegularExpression:
                    return " REGEXP ";
                case Operators.IsIn:
                    return " IN ";
                case Operators.NotIsIn:
                    return " Not IN ";
                default:
                    return " = ";
            }
        }

        public void AppendWhere(string Statement)
        {
            if (string.IsNullOrEmpty(Statement))
            {
                return;
            }
            if (_Where.Length > 0)
            {
                _Where += " AND ";
            }
            _Where += Statement;

        }

        public void AppendWhere(string Field, Operators Op, DateTime Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " AND ";
            }
            if (Op == Operators.Null)
            {
                _Where += Field;
                _Where += " IS NULL";
            }
            else if (Op == Operators.IsIn | Op == Operators.NotIsIn)
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += "(";
                _Where += DateToSQLString(Value);
                _Where += ")";
            }
            else
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += DateToSQLString(Value);
            }
        }

        public void AppendWhere(string Field, Operators Op, string Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " AND ";
            }
            if (Op == Operators.Null)
            {
                if (!bool.Parse(Value))
                {
                    _Where += "NOT ";
                }
                _Where += Field;
                _Where += " IS NULL";
            }
            else if (Op == Operators.IsIn | Op == Operators.NotIsIn)
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += "(";
                _Where += Value;
                _Where += ")";
            }
            else
            {

                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += "'";
                if (Op == Operators.Contains || Op == Operators.EndsWith)
                {
                    _Where += "%";
                }
              _Where += Value.Replace("'", "''");
                if (Op == Operators.StartsWith || Op == Operators.Contains)
                {
                    _Where += "%";
                }

                _Where += "'";
            }
        }

        public void AppendWhere(string Field, Operators Op, decimal Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " AND ";
            }
            _Where += Field;
            _Where += GetOpSign(Op);
            _Where += Value.ToString().Replace(",", ".");
        }

        public void AppendWhere(string Field, Operators Op, long Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " AND ";
            }
            _Where += Field;
            _Where += GetOpSign(Op);
            _Where += Value.ToString();
        }

        public void AppendWhere(string Field, DateTime Value)
        {
            if (_Where.Length > 0)
            {
                _Where += " AND ";
            }
            _Where += Field + " = ";

            _Where += DateToSQLString(Value);

        }

        public string DateToSQLString(DateTime Value)
        {
            if (DataClass == null)
                DataClass = new Data();

            switch (DataClass.Driver)
            {
                case QueryType.MSSQL:
                    return "{d '" + Value.Year + "-" + Value.Month.ToString().PadLeft(2, '0') + "-" + Value.Day.ToString().PadLeft(2, '0') + "'}";
                case QueryType.MYSQL:
                    return "'" + Value.Year + "-" + Value.Month.ToString().PadLeft(2, '0') + "-" + Value.Day.ToString().PadLeft(2, '0') + "'";
                case QueryType.Oracle:
                    return "to_date('" + Value.Year + "-" + Value.Month.ToString().PadLeft(2, '0') + "-" + Value.Day.ToString().PadLeft(2, '0') + "','dd-mm-yyyy'}";
                default:
                    return "";
            }
        }

        public void AppendWhere(OrBuilder ob)
        {
            if (_Where.Length > 0)
            {
                _Where += " AND ";
            }
            _Where += "(" + ob.ToString() + ")";
        }

        public void AppendWhere(string Field, string Value)
        {
            AppendWhere(Field, Operators.Equals, Value);
        }

        public void AppendWhere(string Field, long Value)
        {
            AppendWhere(Field, Operators.Equals, Value);
        }
        #endregion


        #region "OrderBy"
        public void ClearOrderBy()
        {
            _OrderBy = "";
        }

        public void AppendOrderBy(string Fields)
        {
            if (_OrderBy.Length == 0)
            {
                _OrderBy = Fields;
            }
            else
            {
                _OrderBy += "," + Fields;
            }
        }

        public void AppendOrderBy(string Fields, bool Descending)
        {
            if (_OrderBy.Length == 0)
            {
                _OrderBy = Fields;
            }
            else
            {
                _OrderBy += "," + Fields;
            }
            if (Descending)
                _OrderBy += " DESC";
        }

        #endregion

        #region "GroupBy"
        public void AppendGroupBy(string Fields)
        {
            if (_GroupBy.Length == 0)
            {
                _GroupBy = Fields;
            }
            else
            {
                _GroupBy += "," + Fields;
            }
        }

        #endregion

        public override string ToString()
        {
            System.Text.StringBuilder sSQL = new System.Text.StringBuilder();

            sSQL.Append("SELECT ");

            if (_Distinct)
            {
                sSQL.Append(" DISTINCT ");
            }


            if (_Top > 0)
            {
                if (DataClass.Driver == QueryType.Oracle)
                {
                    AppendWhere("Rownum", Operators.LessThanEqual, _Top);
                }
                else
                {
                    sSQL.Append(" TOP ");
                    sSQL.Append(_Top);
                    sSQL.Append(" ");
                }
            }

            sSQL.Append(_Select);
            sSQL.Append(" FROM ");
            sSQL.Append(_From);
            if (_Where.Length > 0)
            {
                sSQL.Append(" WHERE ");
                sSQL.Append(_Where);
            }

            if (_GroupBy.Length > 0)
            {
                sSQL.Append(" GROUP BY ");
                sSQL.Append(_GroupBy);
            }
            if (_Having.Length > 0)
            {
                sSQL.Append(" HAVING ");
                sSQL.Append(_Having);
            }
            if (_OrderBy.Length > 0)
            {
                sSQL.Append(" ORDER BY ");
                sSQL.Append(_OrderBy);
            }
            return sSQL.ToString();
        }

        public List<T> GetDataList<T>() where T : class, new()
        {
            try
            {
                if (DataClass == null)
                {
                    if (HttpContext.Current == null)
                    {
                        DataClass = new Data();
                    }
                    else
                    {
                        Env environment = (Env)HttpContext.Current.Session["Environment"];
                        if (environment == null)
                        {
                            DataClass = new Data();
                        }
                        else
                        {
                            switch (environment.ActiveConnectionType)
                            {
                                case ActiveConnection.Hosting:
                                    DataClass = environment.Connections.HostingConnection;
                                    break;
                                case ActiveConnection.Customer:
                                    DataClass = environment.Connections.CustomerConnection;
                                    break;
                                case ActiveConnection.Workflow:
                                    DataClass = environment.Connections.WorkflowConnection;
                                    break;
                                default:
                                    break;
                            }


                        }
                    }
                }
                
                List<T> result = DataClass.Query<T>(this.ToString());

                if (result == null)
                {
                    throw new Exception(DataClass.LastError, new Exception(this.ToString()));
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception(this.ToString(), ex);
            }
        }

        public DataTable GetData()
        {
            try
            {
                if (DataClass == null)
                {
                    if (HttpContext.Current == null)
                    {
                        DataClass = new Data();
                    }
                    else
                    {
                        Env environment = (Env)HttpContext.Current.Session["Environment"];
                        if (environment == null)
                        {
                            DataClass = new Data();
                        }
                        else
                        {
                            switch (environment.ActiveConnectionType)
                            {
                                case ActiveConnection.Hosting:
                                    DataClass = environment.Connections.HostingConnection;
                                    break;
                                case ActiveConnection.Customer:
                                    DataClass = environment.Connections.CustomerConnection;
                                    break;
                                case ActiveConnection.Workflow:
                                    DataClass = environment.Connections.WorkflowConnection;
                                    break;
                                default:
                                    break;
                            }


                        }
                    }
                }
                DataTable result = DataClass.Query(this.ToString());
                if (result == null)
                {
                    throw new Exception(DataClass.LastError, new Exception(this.ToString()));
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception(this.ToString(), ex);
            }

        }

        public DataTable GetData(bool SkipLog)
        {
            try
            {
                if (DataClass == null)
                {
                    Env environment = (Env)HttpContext.Current.Session["Environment"];
                    if (environment == null)
                        DataClass = new Data();
                    else
                        DataClass = environment.Connections.CustomerConnection;
                }
                if (SkipLog)
                    DataClass.SkipLog = true;

                DataTable result = DataClass.Query(this.ToString());
                if (result == null)
                {
                    throw new Exception(DataClass.LastError, new Exception(this.ToString()));
                }
                return result;
            }
            catch (Exception ex)
            {
                throw new Exception(this.ToString(), ex);
            }

        }



        #region ICloneable Members

        public object Clone()
        {
            QueryBuilder qb = new QueryBuilder();
            qb._Top = _Top;
            qb._Start = _Start;
            qb._Distinct = _Distinct;
            qb._Select = _Select;
            qb._Columns = _Columns;
            qb._From = _From;
            qb._Where = _Where;
            qb._OrderBy = _OrderBy;
            qb._GroupBy = _GroupBy;
            qb._Having = _Having;
            return qb;
        }

        #endregion
    }
}
