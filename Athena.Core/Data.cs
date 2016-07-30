using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using MySql.Data.MySqlClient;
using System.Web;
using System.Net.Mail;
using System.Net;
using System.Collections.Generic;
using System.Reflection;

namespace Athena.Core
{
    public enum QueryType
    {
        MSSQL = 1,
        MYSQL = 2,
        Oracle = 3,
        ODBC = 4
    }

    public enum ConnectionType
    {
        Hosting,
        Customer
    }

    public class Data : IEquatable<Data>
    {
        private string _LastQuery = "";
        private DataTable _LastResultSet;
        private string _LastError = "";

        internal QueryType _Driver = QueryType.MSSQL;
        private readonly ConnectionType _TypeConnection = ConnectionType.Hosting;
        internal string _Database = "";
        internal string _Server = "";
        internal string _Username = "";
        internal string _Password = "";
        internal string _IsHosting = "";
        private string _ConnectionString = "";

        public bool SkipLog { get; set; } = false;


        public string ConnectionString
        {
            get
            {
                return _ConnectionString;
            }
        }

        public string Transaction { get; set; }

        public DataTable LastResultSet
        {
            get { return _LastResultSet; }
        }
        public string LastQuery
        {
            get { return _LastQuery; }
        }
        public string LastError
        {
            get { return _LastError; }
        }


        public QueryType Driver
        {
            get { return _Driver; }
        }

        public ConnectionType TypeConnection
        {
            get { return _TypeConnection; }
        }


        public Data(ConnectionType Type)
        {
            SetData(Type);
        }

        public Data(string connectionString)
        {
            _ConnectionString = connectionString;
            _Driver = QueryType.ODBC;
        }

        public Data(string connectionString, QueryType driver)
        {
            _ConnectionString = connectionString;
            _Driver = driver;
        }

        public string getAspConnection()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Driver={SQL Server}; Server=");
            sb.Append(_Server);
            sb.Append("; Database=");
            sb.Append(_Database);
            sb.Append("; UID=");
            sb.Append(_Username);
            sb.Append("; PWD=");
            sb.Append(_Password);
            sb.Append(";");
            return sb.ToString();
        }


        public string CreateTransaction(Env env)
        {
            _LastError = "";
            this.Transaction = string.Format("TransactionWithUser{0}", env.User.UserID);
            setConnectionString();
            this.OpenConnection(this.Transaction);
            Exec("BEGIN TRANSACTION " + this.Transaction);
            return this.Transaction;
        }

        public void CommitTransaction(string createTransResponse)
        {
            Exec("COMMIT TRANSACTION " + createTransResponse);
            RemoveFromPool(this.Transaction);
            this.Transaction = "";
        }

        public void AbortTransaction(string createTransResponse)
        {
            Exec("ROLLBACK TRANSACTION " + createTransResponse);
            RemoveFromPool(this.Transaction);
            this.Transaction = "";
        }


        private void SetData(ConnectionType Type)
        {
            ConnectionType ct = Type;

            Env environment = null;
            if (HttpContext.Current == null || HttpContext.Current.Session == null)
                ct = ConnectionType.Hosting;
            else
                environment = (Env)HttpContext.Current.Session["Environment"];

            switch (ct)
            {
                case ConnectionType.Hosting:
                    SetHosting();
                    break;
                case ConnectionType.Customer:
                    if (environment == null)
                        SetHosting();
                    else
                    {
                        _Database = environment.Connections.CustomerConnection._Database;
                        _Server = environment.Connections.CustomerConnection._Server;
                        _Password = environment.Connections.CustomerConnection._Password;
                        _Username = environment.Connections.CustomerConnection._Username;
                        _Driver = environment.Connections.CustomerConnection._Driver;
                    }

                    break;
                default:

                    break;
            }

            setConnectionString();
        }

        private void SetHosting()
        {
            System.Configuration.AppSettingsReader asr = new System.Configuration.AppSettingsReader();

            string sDBType = asr.GetValue("DBType", typeof(string)).ToString();
            _Driver = (string.IsNullOrEmpty(sDBType) ? QueryType.MSSQL : (QueryType)Convert.ToInt32(sDBType));

            _IsHosting = asr.GetValue("ISHosting", typeof(string)).ToString();

            if (string.IsNullOrEmpty(_IsHosting))
            {
                _IsHosting = "false";
            }

            _Username = asr.GetValue("userName", typeof(string)).ToString();
            _Password = Encryption.Decrypt(asr.GetValue("userPassword", typeof(string)).ToString());
            _Server = asr.GetValue("serverName", typeof(string)).ToString();
            _Database = asr.GetValue("databaseName", typeof(string)).ToString();
        }

        private void setConnectionString()
        {
            _ConnectionString = string.Format((Driver == QueryType.MSSQL ? "User={0};" : "UID={0};"), _Username);
            _ConnectionString += string.Format("Password={0};Server={1};Database={2};Application Name=Athena_{3};", _Password, _Server, _Database,
                (HttpContext.Current != null ? HttpContext.Current.User.Identity.Name : "Api"));

            if (Driver == QueryType.MSSQL)
            {
                _ConnectionString += "Trusted_Connection=False;";
                if (string.IsNullOrEmpty(this.Transaction))
                {
                    _ConnectionString += "MultipleActiveResultSets=True;";
                }
            }
        }


        public Data()
        {
            SetData(ConnectionType.Customer);
        }

        public Data(string Server, string Database, string UserName, string Password, QueryType Type)
        {
            _Driver = Type;
            _Server = Server;
            _Database = Database;
            _Username = UserName;
            _Password = Password;

            setConnectionString();
        }

        public Data(string Server, string Database, string UserName, string Password)
        {
            _Server = Server;
            _Database = Database;
            _Username = UserName;
            _Password = Password;
            setConnectionString();
        }

        internal string ConvertSQL(string sql)
        {
            string sReturn = sql;
            switch (Driver)
            {
                case QueryType.MYSQL:
                    sReturn = sReturn.Replace("GETDATE", "NOW");
                    sReturn = sReturn.Replace("ISNULL", "IFNULL");
                    sReturn = sReturn.Replace("@", "?");
                    break;
                case QueryType.MSSQL:
                case QueryType.Oracle:
                default:
                    break;
            }
            return sReturn;
        }

        private void FillMSDataSet(string sql, DataSet ds, string datasetName)
        {
            SqlConnection MSConn = OpenConnection(this.Transaction);
            SqlDataAdapter MSAdd = new SqlDataAdapter(sql, MSConn);
            try
            {
                MSAdd.Fill(ds, datasetName);
            }
            catch (Exception ex)
            {
                _LastError = ex.Message;
                ReportError(LastError, sql);
            }
            finally
            {
                MSAdd.Dispose();
              //  MSConn.Close();
             //   MSConn.Dispose();
            }
        }


        public DataSet Query(string sql, string DataSetName)
        {
            if (!this.SkipLog)
            {
                _LastQuery = sql;
            }
            DataSet ds = new DataSet(DataSetName);
            string sQuery = ConvertSQL(sql);

            switch (Driver)
            {
                case QueryType.MSSQL:
                    FillMSDataSet(sQuery, ds, DataSetName);
                    break;
                case QueryType.MYSQL:
                    MySqlConnection MYConn = new MySqlConnection(_ConnectionString);
                    MySqlDataAdapter MYAdd = new MySqlDataAdapter(sQuery, MYConn);
                    try
                    {
                        MYAdd.Fill(ds, DataSetName);
                    }
                    catch (Exception ex)
                    {
                        _LastError = ex.Message;
                        ReportError(LastError, sQuery);

                        MYAdd.Dispose();
                        MYConn.Close();
                        MYConn.Dispose();
                        return null;
                    }
                    finally
                    {
                        MYAdd.Dispose();
                        MYConn.Close();
                        MYConn.Dispose();
                    }
                    break;
                case QueryType.ODBC:
                    System.Data.OleDb.OleDbConnection olConn = new System.Data.OleDb.OleDbConnection(_ConnectionString);
                    System.Data.OleDb.OleDbDataAdapter olAdd = new System.Data.OleDb.OleDbDataAdapter(sQuery, olConn);
                    try
                    {
                        olAdd.Fill(ds, DataSetName);
                    }
                    catch (Exception ex)
                    {
                        _LastError = ex.Message;
                        ReportError(LastError, sQuery);

                        olAdd.Dispose();
                        olConn.Close();
                        olConn.Dispose();

                        return null;
                    }
                    finally
                    {
                        olAdd.Dispose();
                        olConn.Close();
                        olConn.Dispose();
                    }
                    break;
                case QueryType.Oracle:
                default:
                    break;
            }

            if (!this.SkipLog)
            {
                _LastResultSet = ds.Tables[DataSetName];
            }

            return ds;
        }


        /// <summary>
        /// Converts a DataTable to a list with generic objects
        /// </summary>
        /// <typeparam name="T">Generic object</typeparam>
        /// <param name="table">DataTable</param>
        /// <returns>List with generic objects</returns>
        public List<T> Query<T>(string sql) where T : class, new()
        {
            DataTable table = Query(sql);


            try
            {
                List<T> list = new List<T>();

                foreach (var row in table.AsEnumerable())
                {
                    T obj = new T();

                    foreach (var prop in obj.GetType().GetProperties())
                    {
                        try
                        {
                            PropertyInfo propertyInfo = obj.GetType().GetProperty(prop.Name);
                            propertyInfo.SetValue(obj, Convert.ChangeType(row[prop.Name], propertyInfo.PropertyType), null);
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    list.Add(obj);
                }

                return list;
            }
            catch
            {
                return null;
            }
        }



        public DataTable Query(string sql)
        {
            return Query(sql, "Result").Tables["Result"];
        }


        public DataTable GetTableNames()
        {
            DataTable dt = null;
            System.Data.OleDb.OleDbConnection olConn = new System.Data.OleDb.OleDbConnection(_ConnectionString);
            olConn.Open();
            try
            {
                dt = olConn.GetOleDbSchemaTable(System.Data.OleDb.OleDbSchemaGuid.Tables, new object[0]);
            }
            catch (Exception ex)
            {
                _LastError = ex.Message;
                ReportError(LastError, "GetTableNames");
            }
            finally
            {
                olConn.Close();
                olConn.Dispose();
            }
            return dt;
        }

        public void RemoveFromPool(string transaction)
        {
            DataPool.RemoveFromPool(_ConnectionString, transaction);
        }

        public SqlConnection OpenConnection(string transaction)
        {
            SqlConnection Conn = DataPool.FindInPool(_ConnectionString, transaction);
            if (Conn == null || Conn.State != ConnectionState.Open)
            {
                Conn = new SqlConnection(_ConnectionString);

                Conn.Open();
                SqlCommand MSOptions = new SqlCommand(" SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED SET ANSI_NULLS ON SET ARITHABORT ON", Conn);
                MSOptions.ExecuteNonQuery();

                DataPool.AddToPool(Conn, _ConnectionString, transaction);
                //MSOptions.Dispose();
            }

            return Conn;
        }

        public bool Exec(string sql)
        {
            string sQuery = ConvertSQL(sql);
            switch (Driver)
            {
                case QueryType.MSSQL:

                    SqlCommand MSComm = new SqlCommand(sQuery, OpenConnection(this.Transaction));
                    MSComm.CommandTimeout = 0;
                    try
                    {
                        MSComm.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        _LastError = ex.Message;
                        ReportError(LastError, sQuery);

                        MSComm.Connection.Close();
                        MSComm.Dispose();
                        return false;
                    }
                    finally
                    {
                        DataPool.ClearInactiveConnections();
                        //MSComm.Connection.Close();
                    }
                    break;
                case QueryType.MYSQL:
                    MySqlCommand MYComm = new MySqlCommand(sQuery, new MySqlConnection(_ConnectionString));
                    MYComm.Connection.Open();
                    MYComm.CommandTimeout = 0;
                    try
                    {
                        MYComm.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        _LastError = ex.Message;
                        ReportError(LastError, sQuery);


                        return false;
                    }
                    finally
                    {
                        MYComm.Connection.Close();

                    }
                    break;
                case QueryType.Oracle:

                default:
                    break;
            }

            return true;

        }

        internal int ExecWithScope(string sql)
        {
            string sQuery = ConvertSQL(sql);
            string sGetIdentity;

            switch (this.Driver)
            {
                case QueryType.MSSQL:
                    sGetIdentity = "SCOPE_IDENTITY()";
                    break;
                case QueryType.MYSQL:
                    sGetIdentity = "LAST_INSERT_ID()";
                    break;
                case QueryType.Oracle: //TODO: implement
                default:
                    sGetIdentity = "1";
                    break;
            }

            DataTable dt = this.Query(string.Format("{0};SELECT {1}", sQuery, sGetIdentity));
            if (!string.IsNullOrEmpty(LastError))
            {
                throw new Exception(LastError);
            }
            if (dt.Rows.Count > 0 && !string.IsNullOrEmpty(dt.Rows[0][0].ToString()))
                return Convert.ToInt32(dt.Rows[0][0]);
            else
                return 0;

        }

        internal bool Exec(string sql, byte[] File)
        {
            string sQuery = ConvertSQL(sql);
            if (this.Driver == QueryType.MYSQL)
                return ExecMySql(sQuery, File);
            else
            {

                SqlCommand comm = new SqlCommand(sQuery, OpenConnection(this.Transaction));
                SqlParameter param = new SqlParameter("@File", SqlDbType.Image);

                param.Value = File;
                comm.Parameters.Add(param);

                try
                {
                    comm.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    _LastError = ex.Message + " file length: " + File.Length.ToString();
                    ReportError(LastError, sQuery);

                    comm.Connection.Close();
                    comm.Dispose();
                    return false;
                }
                finally
                {
                    comm.Connection.Close();
                    // comm.Dispose();
                }
            }
            return true;
        }

        internal bool ExecMySql(string sql, byte[] File)
        {
            MySqlCommand comm = new MySqlCommand(sql, new MySqlConnection(_ConnectionString));
            MySqlParameter param = new MySqlParameter("?File", MySqlDbType.MediumBlob);
            param.Value = File;
            comm.Parameters.Add(param);

            comm.Connection.Open();
            try
            {
                comm.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _LastError = ex.Message + " file length: " + File.Length.ToString();
                ReportError(LastError, sql);


                return false;
            }
            finally
            {
                comm.Connection.Close();

            }
            return true;
        }

        internal void ReportError(string Description, string Query)
        {
            _LastError = "";
            MailMessage m = new MailMessage();
            try
            {
                m.From = new MailAddress("dennis.jonkers@megatec.nl");
                m.To.Add("dennis.jonkers@megatec.nl");

                m.Subject = string.Format("DATA.CS Foutmelding {0}", HttpContext.Current.Request.Url.AbsoluteUri);
                m.Body = "Er is een foutmelding opgetreden:<BR>";
                m.Body += "Page: <BR>" + HttpContext.Current.Request.Path;
                m.Body += "<BR>";
                m.Body += "Reporterror DATA.CS";
                m.Body += "<BR>";
                m.Body += "Absolute: <BR>" + HttpContext.Current.Request.Url.AbsoluteUri;
                m.Body += "<BR>";
                m.Body += "QueryString: <BR>" + HttpContext.Current.Request.RawUrl;
                m.Body += "<BR>";
                m.Body += "Description: <BR>" + Description;
                m.Body += "<BR>";
                m.Body += "Stack: <BR>" + Query;
                m.Body += "<BR>";
                m.Body += "UserName: <BR>" + HttpContext.Current.User.Identity.Name;
                m.Body += "<BR>";

                Athena.Core.Env environment = (Athena.Core.Env)HttpContext.Current.Session["Environment"];
                if (environment != null)
                {
                    m.Body += "Database: <BR>" + environment.User.DatabaseName;
                    m.Body += "<BR>";
                }
                m.Body += "Query: <BR>" + Query;
                m.Body = m.Body.Replace("/n", "<BR>");

                m.IsBodyHtml = true;


                SmtpClient cl = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential("dennis.jonkers@megatec.nl", "megatec78#1")
                };


                cl.Send(m);
                cl.Dispose();

                if (environment != null)
                {
                    InsertBuilder ib = new InsertBuilder();
                    ib.DataClass = environment.Connections.HostingConnection;
                    ib.Table = "Errors";
                    ib.AppendValue("Page", System.Web.HttpContext.Current.Request.Path);
                    ib.AppendValue("QueryString", System.Web.HttpContext.Current.Request.RawUrl);
                    ib.AppendValue("Description", Description);
                    ib.AppendValue("Stack", Query);
                    ib.AppendValue("UserName", System.Web.HttpContext.Current.User.Identity.Name);
                    ib.AppendValue("LastQuery", environment.Connections.CustomerConnection.LastQuery);
                    if (Tools.DatatableToString(_LastResultSet) != null)
                    {
                        ib.AppendValue("LastResultSet", Tools.DatatableToString(_LastResultSet));
                    }
                    ib.Execute();
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                m.Dispose();
            }

        }


        public bool Equals(Data other)
        {
            return (this._Database.ToUpperInvariant() == other._Database.ToUpperInvariant());
        }
    }

    public static class GuidEncoder
    {
        public static string Encode(string guidText)
        {
            Guid guid = new Guid(guidText);
            return Encode(guid, true);
        }

        public static string Encode(Guid guid, bool decodable)
        {
            string enc = Convert.ToBase64String(guid.ToByteArray());
            if (decodable)
            {
                enc = enc.Replace("/", "_");
                enc = enc.Replace("+", "-");
            }
            else
            {
                enc = enc.Replace("/", "");
                enc = enc.Replace("+", "");
                enc = enc.Replace("=", "");
            }
            return enc;
        }

        public static Guid Decode(string encoded)
        {
            string sEncode = encoded.Replace("_", "/").Replace("-", "+");

            byte[] buffer = Convert.FromBase64String(sEncode + "==");
            return new Guid(buffer);
        }
    }

}
