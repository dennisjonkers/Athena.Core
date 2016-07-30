using System;
using System.Web;

namespace Athena.Core
{
    /// <summary>
    /// Summary description for UpdateBuilder.
    /// </summary>
    public class UpdateBuilder:ICloneable
    {
        public Data DataClass;
        public string _Table = "";
        public string _Where = "";
        public string _Set = "";
        private byte[] _File;

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
            NotEqual,
            RegularExpression,
            IsIn,
            NotIsIn
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

        public void AppendWhere(string Field, string Value)
        {
            AppendWhere(Field, Operators.Equals, Value);
        }

        public void AppendWhere(string Field, long Value)
        {
            AppendWhere(Field, Operators.Equals, Value);
        }
        #endregion

        public void AppendValue(string Field, string Value, bool Function)
        {
            if (Function)
            {
                if (_Set.Length == 0)
                {
                    _Set += Field;
                    _Set += " = ";
                    _Set += Value;
                }
                else
                {
                    _Set += ", ";
                    _Set += Field;
                    _Set += " = ";
                    _Set += Value;
                }
            }
            else
            {
                AppendValue(Field, Value);
            }
        }
        public void AppendValueUnicode(string Field, string Value)
        {
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += Field;
            _Set += " = ";
            _Set += "N'" + Value.Replace("'", "''") + "'";
        }

        public void AppendValue(string Field, string Value)
        {
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += Field;
            _Set += " = ";
            _Set += "'" + Value.Replace("'", "''") + "'";
        }

        public void AppendValue(string Field, System.DateTime Value)
        {
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += Field;
            _Set += " = ";
            _Set += "'" + Value.Year + "-" + Value.Month + "-" + Value.Day + " " + Value.Hour + ":" + Value.Minute + "'";
        }

        public void AppendValue(string Field, decimal Value)
        {
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += Field;
            _Set += " = ";
            _Set += Value.ToString().Replace(',', '.');
        }

        public void AppendValue(string Field, long Value)
        {
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += Field;
            _Set += " = ";
            _Set += Value.ToString();
        }

        public void AppendValue(string Field, bool Value)
        {
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += Field;
            _Set += " = ";
            if (Value)
                _Set += "1";
            else
                _Set += "0";
        }

        public void AppendValue(string Field, byte[] ByteArray)
        {
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += Field;
            _Set += " = ";
            _Set += ByteArray.ToString();
        }


        public void AppendParameter(string ParamField, byte[] Param)
        {
            _File = Param;
            if (_Set.Length > 0)
            {
                _Set += ", ";
            }
            _Set += ParamField;
            _Set += " = @File";
        }

        public bool Execute()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("UPDATE ");
            sb.Append(_Table);
            sb.Append(" SET ");
            sb.Append(_Set);

            if (_SelectQuery != null)
            {
                string sel = _SelectQuery.ToString();
                sel = sel.Substring(sel.IndexOf("FROM"), sel.Length - sel.IndexOf("FROM"));
                sb.Append(" ");
                sb.Append(sel);
            }
            else
            {
                sb.Append(" WHERE ");
                sb.Append(_Where);
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

            bool result;
            if (_File != null)
                result = DataClass.Exec(sb.ToString(), _File);
            else
                result = DataClass.Exec(sb.ToString());

            if (!result)
            {
                throw new Exception(DataClass.LastError);
            }
            return true;
        }

        public enum UpdateType
        {
            Standard,
            NoUser,
            NoExtraFields
        }

        public UpdateBuilder(int UserID)
        {
            if (DataClass == null)
                DataClass = new Data();
            switch (DataClass.Driver)
            {
                case QueryType.MSSQL:
                    AppendValue("SysModified", "GETDATE()", true);
                    break;
                case QueryType.MYSQL:
                    AppendValue("SysModified", "NOW()", true);
                    break;
                case QueryType.Oracle:
                    break;
                default:
                    break;
            }

            AppendValue("SysModifier", UserID);

        }

        public UpdateBuilder()
        {
            Env environment = (Env)HttpContext.Current.Session["Environment"];
            if (DataClass == null)
            {
                if (environment == null)
                    DataClass = new Data();
                else
                    DataClass = environment.Connections.CustomerConnection;
            }
            if (environment == null)
                NewWithType(UpdateType.Standard, 0);
            else
                NewWithType(UpdateType.Standard, environment.User.UserID);
        }

        public UpdateBuilder(UpdateType Type)
        {
            Env environment = (Env)HttpContext.Current.Session["Environment"];
            if (DataClass == null)
            {
                if (environment == null)
                    DataClass = new Data();
                else
                    DataClass = environment.Connections.CustomerConnection;
            }
            if (environment == null)
                NewWithType(Type, 0);
            else
                NewWithType(Type, environment.User.UserID);
        }

        private void NewWithType(UpdateType type, int UserID)
        {
            if (type != UpdateType.NoExtraFields)
            {
                if (DataClass == null)
                    DataClass = new Data();
                switch (DataClass.Driver)
                {
                    case QueryType.MSSQL:
                        AppendValue("SysModified", "GETDATE()", true);
                        break;
                    case QueryType.MYSQL:
                        AppendValue("SysModified", "NOW()", true);
                        break;
                    case QueryType.Oracle:
                        break;
                    default:
                        break;
                }
                if (type == UpdateType.Standard)
                {
                    AppendValue("SysModifier", UserID);
                }
                else
                {
                    AppendValue("SysModifier", 0);
                }
            }
        }

        public object Clone()
        {
            UpdateBuilder ub = new UpdateBuilder();
            ub._Where = _Where;
            ub._Table = _Table;
            ub._Set = _Set;
            return ub;
        }
    }
}
