using System;
using System.Data;
using System.Net;
using System.Net.Mail;
using System.Web;
using System.Web.Security;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Linq;
using System.Web.UI.WebControls;

namespace Athena.Core
{
    public enum ActiveConnection
    {
        Hosting,
        Customer,
        Workflow
    }

    public enum RightType
    {
        View,
        Insert,
        Edit,
        Delete
    }

    /// <summary>
    /// Summary description for Class1.
    /// </summary>
    public class Env
    {

        public UserInfo User;
        public SecurityInfo Security;
        public HistoryInfo History;
        public ConnectionInfo Connections;
        public ActiveConnection ActiveConnectionType = ActiveConnection.Customer;

        public Env()
        {
            User = new UserInfo(this);
            User.Settings = new Dictionary<string, string>();
            Security = new SecurityInfo(this);
            History = new HistoryInfo();
            Connections = new ConnectionInfo();
            Connections.IsHostingConnection = bool.Parse(Connections.HostingConnection._IsHosting);
        }

        public class ConnectionInfo
        {

            public Data HostingConnection = new Data(ConnectionType.Hosting);
            public Data CustomerConnection = new Data(ConnectionType.Hosting);
            public Data WorkflowConnection = new Data(ConnectionType.Hosting);
            public bool IsHostingConnection;
        }

        public class HistoryInfo
        {
            //Used for breadcrumbs
            public List<HistoryElement> _history = new List<HistoryElement>();

            public class HistoryElement
            {
                public string page;
                public string url;

                public HistoryElement(string pageName, string pageUrl)
                {
                    page = pageName;
                    url = pageUrl;
                }
            }

            public string getPreviousUrl()
            {
                System.Configuration.AppSettingsReader asr = new System.Configuration.AppSettingsReader();
                string sReturnurl = asr.GetValue("DefaultPage", typeof(string)).ToString();

                if (_history.Count > 1)
                {
                    if (_history[_history.Count - 1].url != HttpContext.Current.Request.RawUrl &&
                        HttpContext.Current.Request.RawUrl.IndexOf("Buttonbar_Nav") == -1)
                    {
                        sReturnurl = _history[_history.Count - 1].url;
                    }
                    else
                    {
                        sReturnurl = _history[_history.Count - 2].url;
                    }
                }
                else if (_history.Count == 1 && _history.First().url != HttpContext.Current.Request.RawUrl)
                {
                    sReturnurl = _history.First().url;
                }

                return sReturnurl;
            }


            public int colCount
            {
                get { return _history.Count * 2; }
            }

            public void clearAll()
            {
                _history = new List<HistoryElement>();
            }

            public string Output(bool External, bool Crumb)
            {
                string sExtend = "";
                if (External)
                    sExtend = "target='Contents'";

                StringBuilder sOutput = new StringBuilder();
                for (int i = 0; i < _history.Count; i++)
                {
                    if (i == _history.Count - 1 && Crumb)
                    {
                        sOutput.AppendFormat("<li {0}> <a href='#'>{1}</a></li>", sExtend, _history[i].page);
                    }
                    else
                    {
                        sOutput.AppendFormat("<li> <a {0} href='{1}'>{2}</a></li>", sExtend, _history[i].url, _history[i].page);
                    }
                }
                return sOutput.ToString();
            }

            public void addElement(string PageName, string PageUrl)
            {
                int i = 0;
                if (PageUrl.IndexOf("Buttonbar_Nav=True") != -1)
                {
                    _history.Remove(_history[_history.Count - 1]);

                }
                if (PageUrl.IndexOf("DoExcel=1") == -1)
                {
                    while (i < _history.Count)
                    {
                        if (_history[i].page == PageName && _history[i].url == PageUrl)
                        {
                            _history.RemoveRange(i, _history.Count - i);
                            i = _history.Count;
                        }
                        i++;
                    }

                    _history.Add(new HistoryElement(PageName, PageUrl));
                }
            }

        }

        public class SecurityInfo
        {
            private readonly Env _env;

            public SecurityInfo(Env Env)
            {
                _env = Env;
            }
            public bool HasRightsExplicit(string FunctionPoint, RightType Right, List<int> UserSpecific, List<int> CustomerSpecific, List<int> DivisionSpecific)
            {
                if (string.IsNullOrEmpty(FunctionPoint) &&
                   _env.User.SecurityLevel == 1)
                {
                    return false;
                }

                return HasRights(FunctionPoint, Right, UserSpecific, CustomerSpecific, DivisionSpecific);
            }

            public bool HasRights(string FunctionPoint, RightType Right, List<int> UserSpecific, List<int> CustomerSpecific, List<int> DivisionSpecific)
            {
                string sKey = string.Format("Rights_{0}_{1}", FunctionPoint, Right);

                bool bHasRights = false;
                if (FunctionPoint == "")
                {
                    bHasRights = true;
                }
                else
                {

                    if (HttpContext.Current.Session[sKey] != null)
                    {
                        return Convert.ToBoolean(HttpContext.Current.Session[sKey]);

                    }

                    Env environment = (Env)HttpContext.Current.Session["Environment"];
                    QueryBuilder qb = new QueryBuilder();
                    qb.DataClass = environment.Connections.CustomerConnection;
                    qb.AppendSelect("RoleID");
                    qb.AppendFrom("UserRoles");
                    qb.AppendWhere("UserID", environment.User.UserID);
                    DataTable dt = qb.GetData(true);

                    qb = new QueryBuilder();
                    qb.DataClass = (dt.Rows.Count == 0 ? environment.Connections.HostingConnection : environment.Connections.CustomerConnection);

                    qb.AppendSelect("fr.SecurityLevelID");
                    qb.AppendFrom("FunctionRights fr");
                    qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "UserRoles ur", "ur.RoleID = fr.RoleID");
                    qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "FunctionPoints fp", "fp.ID = fr.FunctionPointID");
                    qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "FunctionRightTypes frt", "frt.ID = fr.FunctionRightTypeID");
                    qb.AppendWhere("fp.Name", FunctionPoint);
                    qb.AppendWhere("ur.UserID", _env.User.UserID);
                    qb.AppendWhere("frt.TypeName", Right.ToString());
                    qb.AppendOrderBy("fr.SecurityLevelID");
                    dt = qb.GetData(true);



                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        int SecurityLevel = int.Parse(dt.Rows[i][0].ToString());

                        if (SecurityLevel <= _env.User.SecurityLevel)
                            bHasRights = true;

                        //Customer specific = 9
                        if (SecurityLevel == 9 && CustomerSpecific != null && CustomerSpecific.IndexOf(_env.User.CustomerID) >= 0)
                            bHasRights = true;

                        //User specific = 100
                        if (SecurityLevel == 100 && UserSpecific != null && UserSpecific.IndexOf(_env.User.UserID) >= 0)
                            bHasRights = true;

                        //Division specific = 101
                        if (SecurityLevel == 101 && DivisionSpecific != null && DivisionSpecific.IndexOf(_env.User.Division) >= 0)
                            bHasRights = true;
                    }
                }
                HttpContext.Current.Session[sKey] = bHasRights;
                return bHasRights;
            }

        }

        public class UserInfo
        {
            private readonly Env _env;

            public UserInfo(Env Env)
            {
                _env = Env;
            }

            private string _dbServer = "";
            private string _dbDatabase = "";
            private string _dbUser = "";
            private string _dbPassword = "";
            private readonly QueryType _dbDriver = QueryType.MSSQL;

            private string _UserName = "";
            private int _UserID;
            public string UserPassword = "";
            private int _CustomerID;
            private int _SecurityLevel;
            private string _Roles;
            private int _Division;
            private int _Function;
            private int _FunctionGroup;
            private string _Type;
            private int _ModuleID;
            private string _databaseName = "";
            private int _databaseID;

            public string dbServer
            {
                get { return _dbServer; }
            }

            public string dbDatabase
            {
                get { return _dbDatabase; }
            }

            public string dbUser
            {
                get { return _dbUser; }
            }

            public string dbPassword
            {
                get { return _dbPassword; }
            }

            public QueryType dbDriver
            {
                get { return _dbDriver; }
            }


            public bool IsAuthenticated
            {
                get { return HttpContext.Current.User.Identity.IsAuthenticated; }
            }

            public string Type
            {
                get { return _Type; }
            }

            public int Division
            {
                get { return _Division; }
            }

            public int ModuleID
            {
                get { return _ModuleID; }
            }

            public int DatabaseID
            {
                get { return _databaseID; }
            }

            public string DatabaseName
            {
                get { return _databaseName; }
            }

            public int Function
            {
                get { return _Function; }
            }

            public int FunctionGroup
            {
                get { return _FunctionGroup; }
            }

            public string UserName
            {
                get { return _UserName; }
            }

            public int UserID
            {
                get { return _UserID; }
            }

            public int CustomerID
            {
                get { return _CustomerID; }
            }

            public int SecurityLevel
            {
                get { return _SecurityLevel; }
            }

            public int Language { get; set; }

            public string Roles
            {
                get { return _Roles; }
            }


            public Dictionary<string, string> Settings { get; set; }

            public void LoginAnonymous()
            {

                _UserName = "Guest";
                FormsAuthenticationTicket authTicket = new FormsAuthenticationTicket(1, _UserName, DateTime.Now, DateTime.Now.AddDays(1), false, "Login");
                string encryptedTicket = FormsAuthentication.Encrypt(authTicket);
                HttpCookie authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket);
                try
                {
                    HttpContext.Current.Response.Cookies.Add(authCookie);
                }
                catch (Exception ex)
                {
                    throw ex;
                }

                _UserID = 0;
                _CustomerID = 0;
                _SecurityLevel = 0;
                this.Language = 1;
                _Division = 0;
                _ModuleID = 1;
                _Function = 0;
                _FunctionGroup = 0;
                _databaseName = "Hosting";
                _databaseID = 1;

            }

            public void UserDelegate(int userID, string userName)
            {
                _UserName = userName;
                _UserID = userID;

                System.Configuration.AppSettingsReader asr = new System.Configuration.AppSettingsReader();

                _dbUser = asr.GetValue("userName", typeof(string)).ToString();
                _dbPassword = asr.GetValue("userPassword", typeof(string)).ToString();
                _dbServer = asr.GetValue("serverName", typeof(string)).ToString();
                _dbDatabase = asr.GetValue("databaseName", typeof(string)).ToString();

            }

            public void LoginUser(string userName)
            {
                FormsAuthenticationTicket authTicket = new FormsAuthenticationTicket(1, userName, DateTime.Now, DateTime.Now.AddDays(1), false, "Login");
                string encryptedTicket = FormsAuthentication.Encrypt(authTicket);
                HttpCookie authCookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket);
                try
                {
                    HttpContext.Current.Response.Cookies.Add(authCookie);
                }
                catch (Exception ex)
                {
                    throw ex;
                }

                _UserName = userName.Split(',')[0];
                _UserID = int.Parse(userName.Split(',')[1]);

                Env environment = (Env)HttpContext.Current.Session["Environment"];

                if (environment == null)
                {
                    environment = new Env();
                    HttpContext.Current.Session["Environment"] = environment;
                    return;
                }
                if (!environment.Connections.IsHostingConnection ||
                    _SecurityLevel != 0 ||
                    environment.Connections.CustomerConnection._Database.ToUpper() != environment.Connections.HostingConnection._Database.ToUpper())
                {
                    SetUserInfo(_UserName, null);
                }
                else
                {
                    SetDBInfo(_UserID, _UserName);
                }

            }

            public void SetDBInfo(int dbID)
            {
                Env environment = (Env)HttpContext.Current.Session["Environment"];
                QueryBuilder qb = new QueryBuilder();
                qb.AppendSelect("d.ServerName, d.DatabaseName, d.UserName, d.Password, d.DatabaseType, c.ModuleID, d.Name, d.WorkflowDatabaseID");
                qb.AppendFrom("Databases d");
                qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Customers c", "c.DatabaseID = d.ID");
                qb.AppendWhere("d.ID", dbID);
                qb.DataClass = environment.Connections.HostingConnection;
                System.Data.DataTable dt = qb.GetData(true);

                if (dt.Rows.Count > 0)
                {
                    //Build customerdatabase
                    environment.Connections.CustomerConnection = new Data(dt.Rows[0][0].ToString(),
                     dt.Rows[0][1].ToString(), dt.Rows[0][2].ToString(), dt.Rows[0][3].ToString(),
                     (QueryType)Convert.ToInt32(dt.Rows[0][4]));

                    environment.Connections.WorkflowConnection = (dt.Rows[0][7] == DBNull.Value ?
                        environment.Connections.CustomerConnection : SetWorkflowConnection(Convert.ToInt32(dt.Rows[0][7]), environment));

                    _ModuleID = Convert.ToInt32(dt.Rows[0][5]);
                    _databaseName = dt.Rows[0][6].ToString();
                    _databaseID = dbID;
                }

            }

            private Data SetWorkflowConnection(int dbID, Env environment)
            {
                QueryBuilder qb = new QueryBuilder();
                qb.AppendSelect("d.ServerName, d.DatabaseName, d.UserName, d.Password, d.DatabaseType, c.ModuleID, d.Name");
                qb.AppendFrom("Databases d");
                qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Customers c", "c.DatabaseID = d.ID");
                qb.AppendWhere("d.ID", dbID);
                qb.DataClass = environment.Connections.HostingConnection;
                System.Data.DataTable dt = qb.GetData(true);

                return (dt.Rows.Count > 0 ?
                    new Data(dt.Rows[0][0].ToString(), dt.Rows[0][1].ToString(), dt.Rows[0][2].ToString(), dt.Rows[0][3].ToString(), (QueryType)Convert.ToInt32(dt.Rows[0][4])) :
                    environment.Connections.CustomerConnection);
            }

            public void SetCustomerConnection(Env environment, string UserLogin)
            {
                QueryBuilder qb = new QueryBuilder();
                qb.DataClass = environment.Connections.HostingConnection;
                qb.AppendSelect("ISNULL(dLast.ServerName, d.ServerName), ISNULL(dLast.DatabaseName,d.DatabaseName),ISNULL(dLast.UserName, d.UserName), ISNULL(dLast.Password,d.Password), ISNULL(dLast.DatabaseType,d.DatabaseType), ISNULL(c2.ModuleID,c.ModuleID), ISNULL(dLast.Name,d.Name), ISNULL(dLast.ID,d.ID)");
                qb.AppendFrom("Databases d");
                qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Customers c", "c.DatabaseID = d.ID");
                qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Users u", "u.CustomerID = c.ID");
                qb.AppendFrom(QueryBuilder.JoinType.LeftJoin, "Databases dLast", "dLast.ID=u.LastDB");
                qb.AppendFrom(QueryBuilder.JoinType.LeftJoin, "Customers c2", "c2.DatabaseID = dLast.ID");
                qb.AppendWhere("u.ID", UserLogin.Split(',')[1]);
                System.Data.DataTable dt = qb.GetData(true);

                if (dt.Rows.Count == 1)
                {
                    //Build customerdatabase
                    environment.Connections.CustomerConnection = new Data(dt.Rows[0][0].ToString(),
                     dt.Rows[0][1].ToString(), dt.Rows[0][2].ToString(), dt.Rows[0][3].ToString(),
                     (QueryType)int.Parse(dt.Rows[0][4].ToString()));

                 //   _databaseID = CheckUserDB(Convert.ToInt32(dt.Rows[0][7]), userID);
                }
            }


            private void SetDBInfo(int userID, string userName)
            {
                Env environment = (Env)HttpContext.Current.Session["Environment"];
                QueryBuilder qb = new QueryBuilder();
                qb.DataClass = environment.Connections.HostingConnection;
                qb.AppendSelect("ISNULL(dLast.ServerName, d.ServerName), ISNULL(dLast.DatabaseName,d.DatabaseName),ISNULL(dLast.UserName, d.UserName), ISNULL(dLast.Password,d.Password), ISNULL(dLast.DatabaseType,d.DatabaseType), ISNULL(c2.ModuleID,c.ModuleID), ISNULL(dLast.Name,d.Name), ISNULL(dLast.ID,d.ID)");
                qb.AppendFrom("Databases d");
                qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Customers c", "c.DatabaseID = d.ID");
                qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Users u", "u.CustomerID = c.ID");
                qb.AppendFrom(QueryBuilder.JoinType.LeftJoin, "Databases dLast", "dLast.ID=u.LastDB");
                qb.AppendFrom(QueryBuilder.JoinType.LeftJoin, "Customers c2", "c2.DatabaseID = dLast.ID");
                qb.AppendWhere("u.ID", userID);
                System.Data.DataTable dt = qb.GetData(true);

                if (dt.Rows.Count == 1)
                {

                    //Build customerdatabase
                    environment.Connections.CustomerConnection = new Data(dt.Rows[0][0].ToString(),
                     dt.Rows[0][1].ToString(), dt.Rows[0][2].ToString(), dt.Rows[0][3].ToString(),
                     (QueryType)int.Parse(dt.Rows[0][4].ToString()));

                    _ModuleID = Convert.ToInt32(dt.Rows[0][5]);
                    _databaseName = dt.Rows[0][6].ToString();


                    _databaseID = CheckUserDB(Convert.ToInt32(dt.Rows[0][7]), userID);

                    SetDBInfo(_databaseID);

                    SetUserInfo(userName, _ModuleID);
                }

            }

            private int CheckUserDB(int databaseID, int userID)
            {
                Env environment = (Env)HttpContext.Current.Session["Environment"];
                QueryBuilder qb = new QueryBuilder();
                qb.DataClass = environment.Connections.HostingConnection;
                qb.AppendSelect("DatabaseID");
                qb.AppendFrom("UserDatabases");
                qb.AppendWhere("UserID", userID);
                DataTable dt = qb.GetData();
                foreach (DataRow dr in dt.Rows.OfType<DataRow>())
                {
                    if (Convert.ToInt32(dr[0]) == databaseID)
                    {
                        return databaseID;
                    }
                }

                if (dt.Rows.Count == 0)
                {
                    environment.User.LoginAnonymous();
                    HttpContext.Current.Session.Clear();
                    HttpContext.Current.Session.Abandon();
                    environment.Redirect("http://www.athena-online.nl", false);

                    return 1;
                }
                return Convert.ToInt32(dt.Rows[0][0]);
            }

            private void SetUserInfo(string UserName, int? NewModuleId)
            {
                if (_dbUser == "")
                {
                    System.Configuration.AppSettingsReader asr = new System.Configuration.AppSettingsReader();

                    _dbUser = asr.GetValue("userName", typeof(string)).ToString();
                    _dbPassword = asr.GetValue("userPassword", typeof(string)).ToString();
                    _dbServer = asr.GetValue("serverName", typeof(string)).ToString();
                    _dbDatabase = asr.GetValue("databaseName", typeof(string)).ToString();
                }

                Env environment = (Env)HttpContext.Current.Session["Environment"];
                QueryBuilder qb = new QueryBuilder();
                qb.DataClass = environment.Connections.HostingConnection;
                qb.AppendSelect("u.SecurityLevel, u.LanguageID, u.Division, u.FunctionID, fg.ID, u.Type, u.CustomerID, u.ID, c.ModuleID");
                qb.AppendFrom("Users u");
                qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Customers c", "u.CustomerID = c.ID");
                qb.AppendFrom(QueryBuilder.JoinType.LeftJoin, "Functions f", "f.ID = u.FunctionID");
                qb.AppendFrom(QueryBuilder.JoinType.LeftJoin, "FunctionGroups fg", "fg.ID = f.FunctionGroupID");
                qb.AppendWhere("u.UserName", UserName);
                qb.AppendWhere("u.Type", "U");
                System.Data.DataTable dt = qb.GetData(true);




                if (dt.Rows.Count == 1)
                {
                    qb = new QueryBuilder();
                    qb.DataClass = environment.Connections.CustomerConnection;
                    qb.AppendSelect("u.SecurityLevel");
                    qb.AppendFrom("Users u");
                    qb.AppendWhere("u.ID", Convert.ToInt32(dt.Rows[0][7]));

                    OrBuilder ob = new OrBuilder();
                    ob.AppendWhere("Type", "U");
                    ob.AppendWhere("Type", "A");

                    qb.AppendWhere(ob);
                    System.Data.DataTable dtCust = qb.GetData(true);


                    _UserID = Convert.ToInt32(dt.Rows[0][7]);
                    _SecurityLevel = Convert.ToInt32(dtCust.Rows[0][0]);
                    this.Language = Convert.ToInt32(dt.Rows[0][1]);
                    if (string.IsNullOrEmpty(dt.Rows[0][2].ToString()))
                    {
                        _Division = 0;
                    }
                    else
                    {
                        _Division = Convert.ToInt32(dt.Rows[0][2]);
                    }

                    if (NewModuleId.HasValue)
                    {
                        _ModuleID = NewModuleId.Value;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(dt.Rows[0][8].ToString()))
                        {
                            _ModuleID = 0;
                        }
                        else
                        {
                            _ModuleID = Convert.ToInt32(dt.Rows[0][8]);
                        }
                    }

                    if (string.IsNullOrEmpty(dt.Rows[0][3].ToString()))
                    {
                        _Function = 0;
                    }
                    else
                    {
                        _Function = Convert.ToInt32(dt.Rows[0][3]);
                    }

                    if (string.IsNullOrEmpty(dt.Rows[0][4].ToString()))
                    {
                        _FunctionGroup = 0;
                    }
                    else
                    {
                        _FunctionGroup = Convert.ToInt32(dt.Rows[0][4]);
                    }

                    if (string.IsNullOrEmpty(dt.Rows[0][6].ToString()))
                    {
                        _CustomerID = 0;
                    }
                    else
                    {
                        _CustomerID = Convert.ToInt32(dt.Rows[0][6]);
                    }

                    _Type = dt.Rows[0][5].ToString();
                }

                qb = new QueryBuilder();
                qb.DataClass = environment.Connections.CustomerConnection;
                qb.AppendSelect("RoleID");
                qb.AppendFrom("UserRoles");
                qb.AppendWhere("UserID", UserID);
                dt = qb.GetData(true);
                if (dt.Rows.Count == 0)
                {
                    qb = new QueryBuilder();
                    qb.DataClass = environment.Connections.HostingConnection;
                    qb.AppendSelect("RoleID");
                    qb.AppendFrom("UserRoles");
                    qb.AppendWhere("UserID", UserID);
                    dt = qb.GetData(true);
                }

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    if (i != 0)
                        _Roles += ",";

                    _Roles += dt.Rows[i][0].ToString();
                }
            }
        }

        public void RedirectBack()
        {
            HttpContext.Current.Response.Redirect(this.History.getPreviousUrl(), true);
        }

        public void Redirect(string Page)
        {

            HttpContext.Current.Response.Redirect(Tools.FixUrl(Page), true);
        }

        public void Redirect(string Page, bool FixUrl)
        {
            if (!FixUrl)
            {
                HttpContext.Current.Response.Redirect(Page, true);
            }
            else
            {
                Redirect(Page);
            }
        }

        public void Redirect(string Page, params object[] Values)
        {


            HttpContext.Current.Response.Redirect(string.Format(Tools.FixUrl(Page), Values), true);
        }



    }

    public class Internals
    {
        public static string InternalCountry()
        {
            QueryBuilder qb = new QueryBuilder();
            qb.AppendSelect("c.Code");
            qb.AppendFrom("Customers cus");
            qb.AppendFrom(QueryBuilder.JoinType.InnerJoin, "Countries c", "c.ID=cus.CountryID");
            qb.AppendWhere("cus.ID", Tools.GetSetting("InternalCustomer"));

            return qb.GetData().Rows[0][0].ToString();
        }
    }

    public class Tools
    {

        /// <summary>
        /// Works with MS SQL server only
        /// </summary>
        /// <param name="TableName"></param>
        /// <returns>Returns rowcount of a table</returns>
        public static int GetTableRows(string TableName)
        {
            QueryBuilder qb = new QueryBuilder();
            qb.AppendSelect("SUM(row_count)");
            qb.AppendFrom("sys.dm_db_partition_stats");
            qb.AppendWhere(string.Format("object_id = OBJECT_ID('{0}')", TableName));
            qb.AppendWhere("(index_id = 0 or index_id = 1)");
            DataTable dt = qb.GetData();
            if (dt.Rows.Count > 0)
                return Convert.ToInt32(dt.Rows[0][0]);
            else
                return 0;
        }

        public static string FixUrl(string Url)
        {
            if (!Url.StartsWith("http://", StringComparison.CurrentCultureIgnoreCase) && !Url.StartsWith("javascript:", StringComparison.CurrentCultureIgnoreCase))
            {
                Url = System.Configuration.ConfigurationManager.AppSettings["basepage"] + Url;
            }
            return Url;
        }

        public enum PageSizes
        {
            A4,
            Letter
        }

        public static byte[] MergePDFs(List<byte[]> pdf, PageSizes pageSize, float x, float y)
        {
            byte[] result;

            using (MemoryStream ms = new MemoryStream())
            {

                iTextSharp.text.Document doc = new iTextSharp.text.Document((pageSize == PageSizes.Letter ? iTextSharp.text.PageSize.LETTER : iTextSharp.text.PageSize.A4), x, 1, y, 1);

                iTextSharp.text.pdf.PdfWriter writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, ms);


                doc.Open();

                iTextSharp.text.pdf.PdfContentByte cb = writer.DirectContent;
                iTextSharp.text.pdf.PdfImportedPage page;

                iTextSharp.text.pdf.PdfReader reader = null;
                foreach (byte[] p in pdf)
                {
                    reader = new iTextSharp.text.pdf.PdfReader(p);
                    int pages = reader.NumberOfPages;

                    // loop over document pages
                    for (int i = 1; i <= pages; i++)
                    {
                        doc.NewPage();
                        page = writer.GetImportedPage(reader, i);
                        cb.AddTemplate(page, x, y);
                    }
                }

                doc.Close();

                reader.Dispose();

                result = ms.GetBuffer();
                ms.Flush();
                ms.Dispose();
            }

            return result;
        }


        public static string[] Split(string Line)
        {

            if (string.IsNullOrEmpty(Line))
                return null;
            else
            {
                string[] result = Line.Split(',');
                for (int i = 0; i < result.Length; i++)
                {
                    result[i] = HttpContext.Current.Server.UrlDecode(result[i]);
                }
                return result;
            }

        }

        public static string Join(string[] Array)
        {
            if (Array == null)
                return "";
            else
            {

                for (int i = 0; i < Array.Length; i++)
                {
                    Array[i] = HttpContext.Current.Server.UrlEncode(Array[i]);
                }
                return string.Join(",", Array);
            }
        }

        public static DataTable EnumToDataTable(Type enumType)
        {
            DataTable table = new DataTable();

            //Column that contains the Captions/Keys of Enum        
            table.Columns.Add("Value", typeof(string));
            //Get the type of ENUM for DataColumn
            table.Columns.Add("Id", Enum.GetUnderlyingType(enumType));
            //Add the items from the enum:
            foreach (string name in Enum.GetNames(enumType))
            {
                //Replace underscores with space from caption/key and add item to collection:
                table.Rows.Add(name.Replace('_', ' '), Enum.Parse(enumType, name));
            }

            return table;
        }

        public static string DatatableToString(DataTable dt)
        {
            if (dt != null)
            {
                //Serialize 
                BinaryFormatter bformatter = new BinaryFormatter();
                MemoryStream stream = new MemoryStream();
                dt.TableName = "NewResult";
                bformatter.Serialize(stream, dt);
                string s = Tools.ConvBytesToString(stream.ToArray());
                stream.Close();
                stream.Dispose();
                return s;
            }
            else
            {
                return null;
            }
        }

        public static bool IsMobile()
        {
            return HttpContext.Current.Request.Browser.IsMobileDevice;
        }

        public static DataTable StringToDataTable(string input)
        {
            BinaryFormatter bformatter = new BinaryFormatter();
            MemoryStream stream = new MemoryStream(Tools.ConvStringToBytes(input));
            DataTable dt = (DataTable)bformatter.Deserialize(stream);
            stream.Close();
            stream.Dispose();
            return dt;
        }

        public static decimal ToDecimal(object Value)
        {
            try
            {
                return Convert.ToDecimal(Value.ToString().Replace('.', ','));
            }
            catch (Exception)
            {
                return 0;
            }
        }


        public static bool IsIE6OrLater()
        {

            return ((HttpContext.Current.Request.Browser.IsBrowser("IE")) &&

                (HttpContext.Current.Request.Browser.MajorVersion >= 6));

        }

        public static bool IsFF15OrLater()
        {

            return ((HttpContext.Current.Request.Browser.IsBrowser("Firefox")) &&

                ((HttpContext.Current.Request.Browser.MajorVersion == 1) &&

                (HttpContext.Current.Request.Browser.MinorVersion >= .5) ||

                (HttpContext.Current.Request.Browser.MajorVersion >= 2)));

        }

        public static bool IsSafari() // <-- The  Safari is currently not supported in the latest CTP.
        {

            return (HttpContext.Current.Request.Browser.IsBrowser("Safari"));

        }

        public static byte[] imageToByteArray(System.Drawing.Image imageIn)
        {
            MemoryStream ms = new MemoryStream();
            imageIn.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
            byte[] result = ms.ToArray();
            ms.Dispose();
            return result;
        }

        public static System.Drawing.Image byteArrayToImage(byte[] byteArrayIn)
        {
            MemoryStream ms = new MemoryStream(byteArrayIn);
            System.Drawing.Image returnImage = System.Drawing.Image.FromStream(ms);
            ms.Dispose();
            return returnImage;
        }

        public static System.Drawing.Image resizeImage(System.Drawing.Image imgToResize, Size size)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            Graphics g = Graphics.FromImage((System.Drawing.Image)b);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            g.Dispose();

            System.Drawing.Image img = (System.Drawing.Image)b;
            b.Dispose();

            return img;
        }

        //ISO 8601
        public static int GetWeekNumber(DateTime dt)
        {
            System.Globalization.CultureInfo ciCurr = System.Globalization.CultureInfo.CurrentCulture;
            int weekNum = ciCurr.Calendar.GetWeekOfYear(dt, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return weekNum;
        }

        public static DateTime ISOWeekStart(int Year, int Week)
        {
            DateTime dt = new DateTime(Year, 1, 4); //January 4 is always in week 1 - by definition
            dt = dt.AddDays(-(dt.DayOfWeek - DayOfWeek.Monday + 7) % 7); // Move to the Monday in week 1
            return dt.AddDays(7 * (Week - 1)); // Move to the requested week
        }

        public static int ISOWeekYear(DateTime dt)
        {
            int Week = ISOWeekNumber(dt);
            if (Week >= 52 && dt.Month == 1)
                return dt.Year - 1;
            else if (Week == 1 && dt.Month == 12)
                return dt.Year + 1;
            else
                return dt.Year;
        }

        public static int ISOWeekNumber(DateTime dt)
        {
            int Week = GetWeekNumber(dt);
            if (Week == 53 && dt.Month == 12 && dt.Day > 28 + (dt.DayOfWeek - DayOfWeek.Monday + 7) % 7)
                return 1;
            else
                return Week;
        }


        public static string GenerateNextString(string Code)
        {
            var newString = Regex.Replace(Code, "\\d+", delegate (Match match)
            {
                bool last = match.NextMatch().Index == 0;

                if (last)
                    return (long.Parse(match.Value) + 1).ToString(new string('0', match.Value.Length));
                else
                    return match.Value;
            }, RegexOptions.Compiled);

            return newString;
        }

        public static bool buildXML(string Table, string Folder)
        {
            QueryBuilder qb = new QueryBuilder();
            qb.AppendSelect("Name");
            qb.AppendFrom("SysColumns");
            qb.AppendWhere("object_name(id)", Table);
            qb.AppendWhere("Name", QueryBuilder.Operators.NotEqual, "SysCreated");
            qb.AppendWhere("Name", QueryBuilder.Operators.NotEqual, "SysCreator");
            qb.AppendWhere("Name", QueryBuilder.Operators.NotEqual, "SysModified");
            qb.AppendWhere("Name", QueryBuilder.Operators.NotEqual, "SysModifier");
            DataTable dt = qb.GetData(true);

            if (dt.Rows.Count > 0)
            {
                qb = new QueryBuilder();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    qb.AppendSelect(dt.Rows[i].ItemArray[0].ToString());
                }

                qb.AppendFrom(Table);
                dt = qb.GetData(true);
                dt.TableName = Table;

                dt.WriteXml(Folder + "\\" + Table + ".xml");
                return true;
            }
            else
                return false;


        }

        public static bool IsDateBetween(DateTime Date, string BeginDate, string EndDate)
        {
            int iBegin = int.Parse(BeginDate);
            int iEnd = int.Parse(EndDate);
            int iCurrent = int.Parse(Date.ToString("yyyyMMdd"));

            if (iEnd == 0)
                return (iCurrent >= iBegin);
            else
                return (iCurrent >= iBegin && iCurrent < iEnd);

        }

        public static TimeSpan ConvertToTimeSpan(int Minutes)
        {
            int iMinutes = Minutes;
            int iRest = iMinutes % 60;
            int iHours = iMinutes / 60;
            return new TimeSpan(iHours, iRest, 0);

        }

        public static string ConvertToTime(string Minutes)
        {
            if (Minutes.IndexOf(":") == -1)
            {
                int iMinutes = int.Parse(Minutes);
                int iRest = iMinutes % 60;
                int iHours = iMinutes / 60;
                return iHours.ToString() + ":" + iRest.ToString().PadLeft(2, '0');
            }
            else
            {
                return Minutes;
            }
        }

        public static string ConvertToTime(int Minutes)
        {
            int iRest = Minutes % 60;
            int iHours = Minutes / 60;
            return iHours.ToString() + ":" + iRest.ToString().PadLeft(2, '0');

        }

        public static string DateToString(DateTime Date)
        {
            return Date.ToString("yyyyMMdd");
        }

        public enum Modules
        {
            Base = 1,
            Workflow = 2,
            Activity_Planning = 4,
            Personnel_Planning = 8,
            Banking = 16,
            CRM = 32,
            HRM = 64,
            Logistics = 128,
            Rental = 256,
            Documents = 512,
            Projects = 1024,
            Budget = 2048,
            Financial = 4096,
            Webshop = 8192,
            Invoice = 32768,
            Autos = 65536,
            Hosting = 16384
        }

        public static bool HasModule(Env env, Modules ModuleID)
        {

            return ((env.User.ModuleID & Convert.ToInt32(ModuleID)) == Convert.ToInt32(ModuleID));
        }

        public static bool HasModule(Env env, int ModuleID)
        {
            return ((env.User.ModuleID & ModuleID) == ModuleID);
        }


        public static string GetSetting(string Setting)
        {
            Env env = (Env)HttpContext.Current.Session["Environment"];
            if (env.User.Settings.ContainsKey(Setting))
            {
                return env.User.Settings[Setting];
            }

            QueryBuilder qb = new QueryBuilder();
            qb.AppendSelect("StringValue");
            qb.AppendFrom("Settings");
            qb.AppendWhere("Setting", Setting);
            System.Data.DataTable dt = qb.GetData(true);




            if (dt.Rows.Count > 0)
            {
                if (!env.User.Settings.ContainsKey(Setting))
                {
                    env.User.Settings.Add(Setting, dt.Rows[0][0].ToString());
                }

                return dt.Rows[0][0].ToString();
            }
            else
                return string.Empty;
        }

        public static string GetSetting(string Setting, string DefaultValue)
        {
            string result = GetSetting(Setting);
            if (string.IsNullOrEmpty(result))
            {
                return DefaultValue;
            }
            else
            {
                return result;
            }
        }

        public static void SetSetting(string Setting, string Value)
        {
            Env env = (Env)HttpContext.Current.Session["Environment"];
            if (env.User.Settings.ContainsKey(Setting))
            {
                env.User.Settings[Setting] = Value;
            }
            else
            {
                env.User.Settings.Add(Setting, Value);
            }

            QueryBuilder qb = new QueryBuilder();
            qb.AppendSelect("Setting");
            qb.AppendFrom("Settings");
            qb.AppendWhere("Setting", Setting);

            if (qb.GetData().Rows.Count > 0)
            {
                UpdateBuilder ub = new UpdateBuilder();
                ub.Table = "Settings";
                if (string.IsNullOrEmpty(Value))
                {
                    ub.AppendValue("StringValue", "");
                }
                else
                {
                    ub.AppendValue("StringValue", Value);
                }
                ub.AppendWhere("Setting", Setting);
                ub.Execute();
            }
            else
            {
                InsertBuilder ib = new InsertBuilder();
                ib.Table = "Settings";
                ib.AppendValue("Setting", Setting);
                if (string.IsNullOrEmpty(Value))
                {
                    ib.AppendValue("StringValue", "");
                }
                else
                {
                    ib.AppendValue("StringValue", Value);
                }
                ib.Execute();
            }


        }

        public static string ConvBytesToString(byte[] BitArray)
        {
            return BitConverter.ToString(BitArray);
        }

        public static byte[] ConvStringToBytes(string str)
        {
            string[] sArray = str.Split(new char[] { char.Parse("-") });
            int Counter = sArray.Length;
            byte[] ReturnValue = new byte[Counter];
            for (int i = 0; i < Counter; i++)
            {
                ReturnValue[i] = Convert.ToByte(sArray[i], 0x10);
            }
            return ReturnValue;
        }

        public static string Encrypt(string data)
        {
            return Encryption.Encrypt(data);
        }

        //not used
        private static string Decrypt(string data)
        {
            return data;
        }

        public static Dictionary<string, Dictionary<int, string>> LocalDictionary;

        public class LocalTranslations
        {
            public int DatabaseID { get; set; }
            public int LanguageID { get; set; }
            public Dictionary<int, string> TermDictionary { get; set; }

            public LocalTranslations()
            {
                Env env = (Env)HttpContext.Current.Session["Environment"];
                if (env != null)
                {
                    TermDictionary = new Dictionary<int, string>();
                    DatabaseID = env.User.DatabaseID;
                    LanguageID = env.User.Language;

                    QueryBuilder qb = new QueryBuilder();
                    qb.AppendSelect("Description, TermID");
                    qb.AppendFrom("Translations");
                    qb.AppendWhere("LanguageID", env.User.Language);

                    foreach (DataRow dr in qb.GetData(true).Rows)
                    {
                        TermDictionary.Add(Convert.ToInt32(dr[1]), dr[0].ToString());
                    }
                }
            }
        }

        public static List<LocalTranslations> LocalTerms;

        //If loaded do nothing
        public static string LoadTerm(int TermID)
        {
            Env env = (Env)HttpContext.Current.Session["Environment"];
            if (env != null)
            {
                LocalTranslations ltFound = null;
                bool bFound = false;
                //find list
                foreach (LocalTranslations lt in LocalTerms)
                {
                    if (lt.LanguageID == env.User.Language && lt.DatabaseID == env.User.DatabaseID)
                    {
                        ltFound = lt;
                        bFound = true;
                        break;
                    }
                }
                //build list
                if (!bFound)
                {
                    ltFound = new LocalTranslations();
                    LocalTerms.Add(ltFound);
                }
                //getterm
                string returnValue = "";
                ltFound.TermDictionary.TryGetValue(TermID, out returnValue);
                return returnValue;

            }
            return "";
        }
        public static Dictionary<string, Dictionary<int, string>> TermDictionary;

        public static bool NotEmpty(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        public static bool Empty(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }




        public static string GetTerm(string caption, string captionID)
        {
            Env env = (Env)HttpContext.Current.Session["Environment"];
            if (env != null)
            {
                return GetTerm(caption, captionID, env.User.Language);
            }
            return caption;
        }

        public static string GetTerm(string caption, string captionID, int LanguageID)
        {
            if (captionID == "0")
            {
                return caption;
            }
            if (LocalTerms == null)
            {
                LocalTerms = new List<LocalTranslations>();
            }
            Regex regex = new Regex("(\\d)+");
            MatchCollection mc = regex.Matches(captionID);

            Regex regex2 = new Regex("(\\w)+");
            MatchCollection mcWords = regex2.Matches(caption);

            if (TermDictionary == null)
                TermDictionary = new Dictionary<string, Dictionary<int, string>>();

            for (int i = 0; i < mc.Count; i++)
            {
                bool bFound = false;

                //Check for local first

                string sFoundLocal = (Convert.ToInt32(mc[i].Value) == 0 && mcWords.Count > i ? mcWords[i].Value : LoadTerm(Convert.ToInt32(mc[i].Value)));
                if (!string.IsNullOrEmpty(sFoundLocal))
                {
                    bFound = true;
                    captionID = regex.Replace(captionID, sFoundLocal, 1);
                }
                else if (TermDictionary.ContainsKey(mc[i].Value))
                {
                    Dictionary<int, string> findObject;
                    TermDictionary.TryGetValue(mc[i].Value, out findObject);
                    if (findObject.ContainsKey(LanguageID))
                    {
                        bFound = true;
                        string sValue;
                        findObject.TryGetValue(LanguageID, out sValue);
                        captionID = regex.Replace(captionID, sValue, 1);
                    }
                }

                if (!bFound && mc[i].Value != "0")
                {
                    Env environment = (Env)HttpContext.Current.Session["Environment"];
                    Data DataClass = null;
                    if (environment != null)
                        DataClass = environment.Connections.HostingConnection;
                    else
                        DataClass = new Data();

                    System.Data.DataTable dt = null;
                    switch (DataClass.Driver)
                    {
                        case QueryType.MSSQL:
                            dt = DataClass.Query("EXEC GETTERM " + mc[i].Value + "," + (LanguageID == 0 ? "1" : LanguageID.ToString()));
                            break;
                        case QueryType.MYSQL:
                            QueryBuilder qb = new QueryBuilder();

                            qb.AppendSelect("Description");
                            qb.AppendFrom("Translations");
                            qb.AppendWhere("TermID", mc[i].Value);
                            qb.AppendWhere("LanguageID", LanguageID);

                            dt = qb.GetData(true);
                            break;
                        case QueryType.Oracle:
                            break;
                        default:
                            break;
                    }


                    if (dt != null && dt.Rows.Count == 1)
                    {
                        if (!TermDictionary.ContainsKey(mc[i].Value))
                        {
                            Dictionary<int, string> translation = new Dictionary<int, string>();
                            translation.Add(LanguageID, HttpUtility.HtmlDecode(dt.Rows[0].ItemArray[0].ToString()));
                            TermDictionary.Add(mc[i].Value, translation);
                        }
                        else
                        {
                            Dictionary<int, string> findObject;
                            TermDictionary.TryGetValue(mc[i].Value, out findObject);
                            if (!findObject.ContainsKey(LanguageID))
                            {
                                findObject.Add(LanguageID, HttpUtility.HtmlDecode(dt.Rows[0].ItemArray[0].ToString()));
                            }
                        }
                        captionID = regex.Replace(captionID, HttpUtility.HtmlDecode(dt.Rows[0].ItemArray[0].ToString()), 1);
                    }
                    else
                        return caption;
                }


            }
            return captionID;
        }



        public static decimal FormatNumber(string Value)
        {
            if (Value == "")
                return 0;

            Value = Value.Replace(",", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            Value = Value.Replace(".", System.Globalization.CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            return decimal.Parse(Value);
        }


        /// <summary>
        /// Reports error to the database and to the system account
        /// </summary>
        /// <param name="ex">This is the exception</param>
        /// <returns>Returns a string if this function is not failsafe</returns>
        public static string ReportError(Exception ex)
        {
            MailMessage m = new MailMessage();
            try
            {

                m.From = new MailAddress(Tools.GetSetting("SystemEmail"));
                m.To.Add("dennis.jonkers@megatec.nl");

                m.Subject = "Foutmelding";
                m.Body = "Er is een foutmelding opgetreden:<BR>";
                m.Body += "Page: <BR>" + HttpContext.Current.Request.Path;
                m.Body += "<BR>";
                m.Body += "QueryString: <BR>" + HttpContext.Current.Request.RawUrl;
                m.Body += "<BR>";
                m.Body += "Description: <BR>" + ex.Message;
                m.Body += "<BR>";
                m.Body += "Stack: <BR>" + ex.StackTrace;
                m.Body += "<BR>";
                m.Body += "UserName: <BR>" + HttpContext.Current.User.Identity.Name;
                m.Body += "<BR>";
                m.Body += "Query: <BR>" + ex.GetBaseException().Message;
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
            }
            catch (Exception ex3)
            {
                return ex3.Message;
            }
            finally
            {
                m.Dispose();
            }

            InsertBuilder ib = new InsertBuilder();
            ib.DataClass = new Data(ConnectionType.Hosting);
            string sLasterror = ib.DataClass.LastError;
            try
            {
                ib.Table = "Errors";
                ib.AppendValue("Page", HttpContext.Current.Request.Path);
                ib.AppendValue("QueryString", HttpContext.Current.Request.RawUrl);
                ib.AppendValue("Description", ex.Message);
                ib.AppendValue("Stack", ex.StackTrace);
                ib.AppendValue("UserName", HttpContext.Current.User.Identity.Name);
                ib.Execute();
            }
            catch (Exception)
            {
            }



            return "";
        }


        /// <summary>
        /// Validates the code against the table and returns if alrewady exists
        /// </summary>
        /// <param name="Table">The table to search in</param>
        /// <param name="Code">The code to search for</param>
        /// <param name="qb">Extra where clauses or things like that, if used leave out the table and code</param>
        /// <returns>If the code is already existing</returns>
        public static bool ValidateCode(string Table, string Code, QueryBuilder qb)
        {
            qb.AppendSelect("Code");
            qb.AppendFrom(Table);
            qb.AppendWhere("Code", Code);

            return qb.GetData().Rows.Count == 0;
        }

        /// <summary>
        /// Validates the code against the table and returns if alrewady exists
        /// </summary>
        /// <param name="Table">The table to search in</param>
        /// <param name="Code">The code to search for</param>
        /// <returns>If the code is already existing</returns>
        public static bool ValidateCode(string Table, string Code)
        {
            return ValidateCode(Table, Code, new QueryBuilder());
        }

        public static void AddLog(string Type)
        {
            if (System.Web.HttpContext.Current.Request.ServerVariables == null ||
                 System.Web.HttpContext.Current.Request.UserHostAddress == null)
                return;

            string sBrowser = System.Web.HttpContext.Current.Request.ServerVariables["HTTP_USER_AGENT"].ToString();

            string sIP = System.Web.HttpContext.Current.Request.UserHostAddress.ToString();
            string sPage = System.Web.HttpContext.Current.Request.Url.ToString();
            string sOS = System.Web.HttpContext.Current.Request.Browser.Platform.ToString();

            sOS = FindOS(sOS, sBrowser); // finding os through findos function
            sBrowser = FindBrowser(sBrowser); // finding browser through findbrowser function


            string sPageRef = "";
            if (System.Web.HttpContext.Current.Request.UrlReferrer != null)
            {
                sPageRef = System.Web.HttpContext.Current.Request.UrlReferrer.ToString();
            }
            else
            {
                sPageRef = "Direct Visit"; // because variable cannot accept null value, may error in null exception error
            }


            InsertBuilder ib = new InsertBuilder();
            ib.Table = "HitStatistics";
            ib.AppendValue("IP", sIP);
            ib.AppendValue("Page", sPage);
            ib.AppendValue("Ref", sPageRef);
            ib.AppendValue("Browser", sBrowser);
            ib.AppendValue("OS", sOS);
            ib.AppendValue("Count", 1);
            ib.AppendValue("Type", Type);
            ib.Execute();
        }

        private static string FindBrowser(string Browser)
        {
            string _strBrowser;
            if (Browser.Contains("Chrome"))
            {
                _strBrowser = "Chrome";


            }
            else if (Browser.Contains("Avant Browser"))
            {
                _strBrowser = "Avant Browser";


            }
            else if (Browser.Contains("Googlebot"))
            {
                _strBrowser = "Googlebot";


            }
            else if (Browser.Contains("Yahoo! Slurp"))
            {
                _strBrowser = "Yahoo! Slurp";


            }
            else if (Browser.Contains("Mediapartners-Google"))
            {
                _strBrowser = "Mediapartners-Google";


            }
            else if (Browser.Contains("msnbot"))
            {
                _strBrowser = "msnbot";


            }
            else if (Browser.Contains("SurveyBot"))
            {
                _strBrowser = "SurveyBot/2.3 (Whois Source)";


            }
            else if (Browser.Contains("Baiduspider"))
            {
                _strBrowser = "Baiduspider";


            }
            else if (Browser.Contains("FeedFetcher-Google"))
            {
                _strBrowser = "FeedFetcher-Google";


            }
            else if (Browser.Contains("ia_archiver"))
            {
                _strBrowser = "ia_archiver";


            }
            else
            {
                _strBrowser = System.Web.HttpContext.Current.Request.Browser.Browser.ToString();
                _strBrowser += " " + System.Web.HttpContext.Current.Request.Browser.Version.ToString();

            }
            return _strBrowser;

        }

        private static string FindOS(string OS, string Browser)
        {
            string _stros = OS;

            if (OS.Contains("Win"))
            {
                if (Browser.Contains("Windows NT 6.1"))
                {
                    _stros = "Windows 7";

                }

                if (Browser.Contains("Windows NT 6.0"))
                {
                    if (OS.Contains("Media Center PC 5.0"))
                    {
                        _stros = "Windows Vista";
                    }
                    else
                    {
                        _stros = "Windows 2008 Server";

                    }
                }

                if (Browser.Contains("Windows NT 5.2"))
                {
                    _stros = "Windows 2003 Server/ XP 64-BIT";
                }
                if (Browser.Contains("Windows NT 5.1"))
                {
                    _stros = "Windows XP";
                }
                if (Browser.Contains("Windows NT 5.01"))
                {
                    _stros = "Windows 2000 SP1";
                }
                if (Browser.Contains("Windows NT 5.0"))
                {
                    _stros = "Windows 2000";
                }
                if (Browser.Contains("Windows NT 4.0"))
                {
                    _stros = "Windows NT 4.0";
                }
                if (Browser.Contains("Windows 98"))
                {
                    _stros = "Windows 98";
                }
                if (Browser.Contains("Windows 95"))
                {
                    _stros = "Windows 95";
                }
                if (Browser.Contains("Windows CE"))
                {
                    _stros = "Windows CE";
                }
            }
            else if (Browser.Contains("Mac OS X"))
            {
                if (Browser.Contains("iPhone"))
                {
                    _stros = "iPhone-Mac OS X";
                }
                else
                {
                    _stros = "Mac OS X";
                }

            }
            else if (OS.Contains("UNIX"))
            {
                if (Browser.Contains("FreeBSD"))
                {
                    _stros = "FreeBSD";
                }
                else if (Browser.Contains("Linux"))
                {
                    if (Browser.Contains("Ubuntu"))
                    {
                        _stros = "Ubuntu";
                    }
                    else if (Browser.Contains("Fedora"))
                    {
                        _stros = "Fedora";
                    }
                    else if (Browser.Contains("CentOS"))
                    {
                        _stros = "CentOS";
                    }
                    else if (Browser.Contains("Red Hat"))
                    {
                        _stros = "Red Hat";
                    }
                    //add more linux versions here
                    else
                    {
                        _stros = "Linux";
                    }
                }
                else
                {
                    _stros = "UNIX";
                }
            }

            else if (OS.Contains("Unknown"))
            {
                _stros = OS;
            }

            return _stros;
        }
    }
}
