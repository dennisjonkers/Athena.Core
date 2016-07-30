using System;
using System.Data;
using System.Web;

namespace Athena.Core
{
	/// <summary>
	/// Summary description for QueryBuilder.
	/// </summary>
	public class UnionBuilder 
	{
        public Data DataClass;

        private QueryBuilder _qb1;
        private QueryBuilder _qb2;
        private string _OrderBy = "";

        public UnionBuilder(QueryBuilder qb1, QueryBuilder qb2, string OrderBy)
        {
            _qb1 = qb1;
            _qb2 = qb2;
            _OrderBy = OrderBy;
        }

        public UnionBuilder(QueryBuilder qb1, QueryBuilder qb2)
        {
            _qb1 = qb1;
            _qb2 = qb2;
        }

        public override string ToString()
        {
            System.Text.StringBuilder sSQL = new System.Text.StringBuilder();

            _qb1._OrderBy = "";
            _qb2._OrderBy = "";

            sSQL.Append(_qb1.ToString());
            sSQL.Append(" UNION ALL ");
            sSQL.Append(_qb2.ToString());
            if (!string.IsNullOrWhiteSpace(_OrderBy))
            {
                sSQL.Append(" ORDER BY ");
                sSQL.Append(_OrderBy);
            }
            return sSQL.ToString();
        }

		public DataTable GetData() 
		{
            if (DataClass == null)
            {
                Env environment = (Env)HttpContext.Current.Session["Environment"];
                if (environment == null)
                    DataClass = new Data();
                else
                    DataClass = environment.Connections.CustomerConnection;
            }

            DataTable result = DataClass.Query(this.ToString());
			if (result == null) 
			{
                throw new Exception(DataClass.LastError); 
			} 
			return result; 
		}


      
    } 
}
