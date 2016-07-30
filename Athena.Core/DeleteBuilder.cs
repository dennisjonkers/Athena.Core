using System;
using System.Web;

namespace Athena.Core
{
    /// <summary>
    /// Summary description for DeleteBuilder.
    /// </summary>
    public class DeleteBuilder
    {
        public Data DataClass;

        public DeleteBuilder()
        {
        }

        public DeleteBuilder(Data dClass)
        {
            DataClass = dClass;
        }

        public DeleteBuilder(string Table, string Field, long Value)
        {
            this._Table = Table;
            this.AppendWhere(Field, Value);
        }

        public DeleteBuilder(string Table, string Field, string Value)
        {
            this._Table = Table;
            this.AppendWhere(Field, Value);
        }

        private string _Table = "";
        private string _Where = "";
        private QueryBuilder _SelectQuery = null;

        #region "Table"
        public string Table
        {
            get { return _Table; }
            set { _Table = value; }
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
            NotEqual
        }

        private string GetOpSign(Operators op)
        {
            string sReturn;
            switch (op)
            {
                case Operators.NotEqual:
                    sReturn = " <> ";
                    break;
                case Operators.GreaterThan:
                    sReturn = " > ";
                    break;
                case Operators.GreaterThanEqual:
                    sReturn = " >= ";
                    break;
                case Operators.LessThan:
                    sReturn = " < ";
                    break;
                case Operators.LessThanEqual:
                    sReturn = " <= ";
                    break;
                case Operators.StartsWith:
                    sReturn = " LIKE ";
                    break;
                case Operators.Equals:
                default:
                    sReturn = " = ";
                    break;

            }
            return sReturn;
        }

        public void AppendQueryBuilder(QueryBuilder SelectQuery)
        {
            _SelectQuery = SelectQuery;
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
            else
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += "'";
                _Where += Value;
                if (Op == Operators.StartsWith)
                {
                    _Where += "%";
                }
                _Where += "'";
            }
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
            else
            {
                _Where += Field;
                _Where += GetOpSign(Op);
                _Where += DateToSQLString(Value);
            }
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
                case QueryType.Oracle: //implement
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
        #endregion

        public bool Execute()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("DELETE ");
            if (_SelectQuery != null)
            {
                sb.Append(_Table);
                string sel = _SelectQuery.ToString();
                sel = sel.Substring(sel.IndexOf("FROM"), sel.Length - sel.IndexOf("FROM"));
                sb.Append(" ");
                sb.Append(sel);
            }
            else
            {
                sb.Append("FROM ");
                sb.Append(_Table);
                if (_Where.Length > 0)
                {
                    sb.Append(" WHERE ");
                    sb.Append(_Where);
                }
            }

            if (DataClass == null)
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

            bool result = DataClass.Exec(sb.ToString());
            if (!result)
            {
                throw new Exception(DataClass.LastError);
            }
            return true;
        }

    }
}
