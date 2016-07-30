using System;
using System.Web;
using System.Security.Principal;

namespace Athena.Core
{
    /// <summary>
    /// Summary description for InsertBuilder.
    /// </summary>
    public class InsertBuilder : ICloneable 
    {
        public Data DataClass;
        private QueryBuilder _SelectQuery;
        private string _SelectFields = "";
        public string _Table = "";
        public string _Fields = "";
        public string _Values = "";
        private byte[] _File;
        public string Table
        {
            get { return _Table; }
            set { _Table = value; }
        }

        public void AppendQueryBuilder(string SelectFields, QueryBuilder SelectQuery)
        {
            _SelectFields = SelectFields;
            _SelectQuery = SelectQuery;
        }

        public void AppendValue(string Field, string Value, bool Function)
        {
            if (Function)
            {
                if (_Fields.Length == 0)
                {
                    _Fields = Field;
                    _Values = Value;
                }
                else
                {
                    _Fields += "," + Field;
                    _Values += "," + Value;
                }
            }
            else
            {
                AppendValue(Field, Value);
            }
        }

        public void AppendValue(string Field, string Value)
        {
            if (_Fields.Length == 0)
            {
                _Fields = Field;
                _Values = "'" + Value.Replace("'", "''") + "'";
            }
            else
            {
                _Fields += "," + Field;
                _Values += ",'" + Value.Replace("'", "''") + "'";
            }
        }


        public void AppendValueUnicode(string Field, string Value)
        {
            if (_Fields.Length == 0)
            {
                _Fields = Field;
                _Values = "N'" + Value.Replace("'", "''") + "'";
            }
            else
            {
                _Fields += "," + Field;
                _Values += ",N'" + Value.Replace("'", "''") + "'";
            }
        }

        public void AppendValue(string Field, System.DateTime Value)
        {
            if (_Fields.Length > 0)
            {
                _Fields += ", ";
            }
            _Fields += Field;
            if (_Values.Length > 0)
            {
                _Values += ", ";
            }
            _Values += "'" + Value.Year + "-" + Value.Month + "-" + Value.Day + " " + Value.Hour + ":" + Value.Minute + "'";

        }

        public void AppendValue(string Field, byte[] ByteArray)
        {
            if (_Fields.Length == 0)
            {
                _Fields = Field;
                _Values = ByteArray.ToString();
            }
            else
            {
                _Fields += "," + Field;
                _Values += "," + ByteArray.ToString();
            }
        }

        public void AppendValue(string Field, bool Value)
        {
            if (_Fields.Length == 0)
            {
                _Fields = Field;
                if (Value)
                    _Values = "1";
                else
                    _Values = "0";
            }
            else
            {
                _Fields += "," + Field;
                _Values += ",";
                if (Value)
                    _Values += "1";
                else
                    _Values += "0";

            }
        }

        public void AppendValue(string Field, decimal Value)
        {
            if (_Fields.Length == 0)
            {
                _Fields = Field;
                _Values = Value.ToString().Replace(',', '.');
            }
            else
            {
                _Fields += "," + Field;
                _Values += "," + Value.ToString().Replace(',', '.');
            }
        }

        public void AppendValue(string Field, long Value)
        {
            if (_Fields.Length == 0)
            {
                _Fields = Field;
                _Values = Value.ToString();
            }
            else
            {
                _Fields += "," + Field;
                _Values += "," + Value;
            }
        }

        public void AppendParameter(string ParamField, byte[] Param)
        {
            _File = Param;
            if (_Fields.Length == 0)
            {
                _Fields = ParamField;
                _Values = "@File";
            }
            else
            {
                _Fields += "," + ParamField;
                _Values += ",@File";
            }
        }

        /// <summary>
        /// Insert a row into the table and return its ID
        /// Autonumber field ID is returned
        /// </summary>
        /// <returns></returns>
        public int ExecuteIdentity()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO ");
            sb.Append(_Table);

            sb.Append("(");
            if (_SelectQuery == null)
                sb.Append(_Fields);
            else
                sb.Append(_SelectFields);
            sb.Append(") ");

            if (_SelectQuery == null)
            {
                sb.Append("VALUES (");
                sb.Append(_Values);
                sb.Append(")");
            }
            else
                sb.Append(_SelectQuery.ToString());

           
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

            return DataClass.ExecWithScope(sb.ToString());
        }

        /// <summary>
        /// Try to Insert a row with a specified ID
        /// This will ignore the identity seeding
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public bool Execute(int ID, string IDColumnName)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("SET IDENTITY_INSERT ");
            sb.Append(_Table);
            sb.Append(" ON;INSERT INTO ");
            sb.Append(_Table);

            sb.Append("(" + IDColumnName + ",");
            if (_SelectQuery == null)
                sb.Append(_Fields);
            else
                sb.Append(_SelectFields);
            sb.Append(") ");

            if (_SelectQuery == null)
            {
                sb.Append("VALUES (");
                sb.Append(ID);
                sb.Append(",");
                sb.Append(_Values);
                sb.Append(")");
            }
            else
                sb.Append(_SelectQuery.ToString());

            sb.Append(";SET IDENTITY_INSERT ");
            sb.Append(_Table);
            sb.Append(" OFF;");

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
                if (DataClass.LastError.IndexOf("duplicate key") > -1)
                {
                    throw new ExistsException(DataClass.LastError);
                }
                else
                {
                    throw new Exception(DataClass.LastError);
                }
            }
            return true;

        }

        /// <summary>
        /// Try to Insert a row with a specified ID
        /// This will ignore the identity seeding
        /// </summary>
        /// <param name="ID"></param>
        /// <returns></returns>
        public bool Execute(int ID)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("SET IDENTITY_INSERT ");
            sb.Append(_Table);
            sb.Append(" ON;INSERT INTO ");
            sb.Append(_Table);

            sb.Append("(ID,");
            if (_SelectQuery == null)
                sb.Append(_Fields);
            else
                sb.Append(_SelectFields);
            sb.Append(") ");

            if (_SelectQuery == null)
            {
                sb.Append("VALUES (");
                sb.Append(ID);
                sb.Append(",");
                sb.Append(_Values);
                sb.Append(")");
            }
            else
                sb.Append(_SelectQuery.ToString());

            sb.Append(";SET IDENTITY_INSERT ");
            sb.Append(_Table);
            sb.Append(" OFF;");
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
                if (DataClass.LastError.IndexOf("duplicate key") > -1)
                {
                    throw new ExistsException(DataClass.LastError);
                }
                else
                {
                    throw new Exception(DataClass.LastError);
                }
            }
            return true;

        }

        public bool Execute()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO ");
            sb.Append(_Table);

            sb.Append("(");
            if (_SelectQuery == null)
                sb.Append(_Fields);
            else
                sb.Append(_SelectFields);
            sb.Append(") ");

            if (_SelectQuery == null)
            {
                sb.Append("VALUES (");
                sb.Append(_Values);
                sb.Append(")");
            }
            else
                sb.Append(_SelectQuery.ToString());

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
                if (DataClass.LastError.IndexOf("duplicate key") > -1)
                {
                    throw new ExistsException(DataClass.LastError);
                }
                else
                {
                    throw new Exception(DataClass.LastError);
                }
            }
            return true;
        }

        public enum InserType
        {
            Standard,
            NoUser,
            NoExtraFields
        }

        public InsertBuilder(int UserID, Data dc)
        {
            DataClass = dc;
            switch (DataClass.Driver)
            {
                case QueryType.MSSQL:
                    AppendValue("SysModified", "GETDATE()", true);
                    AppendValue("SysCreated", "GETDATE()", true);
                    break;
                case QueryType.MYSQL:
                    AppendValue("SysModified", "NOW()", true);
                    AppendValue("SysCreated", "NOW()", true);
                    break;
                case QueryType.Oracle:
                    break;
                default:
                    break;
            }
            AppendValue("SysModifier", UserID);
            AppendValue("SysCreator", UserID);
        }

        public InsertBuilder(int UserID)
        {
            if (HttpContext.Current == null)
            {
                DataClass = new Data();
            }
            else
            {
                Env environment = (Env)HttpContext.Current.Session["Environment"];
                if (DataClass == null)
                {

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

            switch (DataClass.Driver)
            {
                case QueryType.MSSQL:
                    AppendValue("SysModified", "GETDATE()", true);
                    AppendValue("SysCreated", "GETDATE()", true);
                    break;
                case QueryType.MYSQL:
                    AppendValue("SysModified", "NOW()", true);
                    AppendValue("SysCreated", "NOW()", true);
                    break;
                case QueryType.Oracle:
                    break;
                default:
                    break;
            }
            AppendValue("SysModifier", UserID);
            AppendValue("SysCreator", UserID);
        }

        public InsertBuilder()
        {
            Env environment = null;
            if (HttpContext.Current.Session!=null)
                environment = (Env)HttpContext.Current.Session["Environment"];

            if (DataClass == null)
            {

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
            if (environment == null)
                NewWithType(InserType.Standard, 0);
            else
                NewWithType(InserType.Standard, environment.User.UserID);


        }

        public InsertBuilder(InserType Type)
        {
            if (HttpContext.Current == null)
            {
                DataClass = new Data();
            }
            else
            {
                Env environment = (Env)HttpContext.Current.Session["Environment"];
                if (DataClass == null)
                {

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
                if (environment == null)
                    NewWithType(Type, 0);
                else
                    NewWithType(Type, environment.User.UserID);
            }
           

        }

        private void NewWithType(InserType type, int UserID)
        {
            if (type != InserType.NoExtraFields)
            {
                switch (DataClass.Driver)
                {
                    case QueryType.MSSQL:
                        AppendValue("SysModified", "GETDATE()", true);
                        AppendValue("SysCreated", "GETDATE()", true);
                        break;
                    case QueryType.MYSQL:
                        AppendValue("SysModified", "NOW()", true);
                        AppendValue("SysCreated", "NOW()", true);
                        break;
                    case QueryType.Oracle:
                        break;
                    default:
                        break;
                }

                if (type == InserType.Standard)
                {
                    AppendValue("SysCreator", UserID);
                    AppendValue("SysModifier", UserID);
                }
                else
                {
                    AppendValue("SysCreator", 0);
                    AppendValue("SysModifier", 0);
                }
            }
        }

        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("INSERT INTO ");
            sb.Append(_Table);

            sb.Append("(");
            if (_SelectQuery == null)
                sb.Append(_Fields);
            else
                sb.Append(_SelectFields);
            sb.Append(") ");

            if (_SelectQuery == null)
            {
                sb.Append("VALUES (");
                sb.Append(_Values);
                sb.Append(")");
            }
            else
                sb.Append(_SelectQuery.ToString());

            return sb.ToString();
        }

        #region ICloneable Members

        public object Clone()
        {
            InsertBuilder ib = new InsertBuilder();
            ib._Fields = _Fields;
            ib._Table = _Table;
            ib._Values = _Values;
            return ib;
        }

        #endregion
    }
}
