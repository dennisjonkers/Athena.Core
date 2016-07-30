using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;

namespace Athena.Core
{
    public class DataConnection
    {
        public SqlConnection connection { get; set; }
        public DateTime lastAccess { get; set; }
        public string connectionString { get; set; }
        public string transaction { get; set; }
    }

    public static class DataPool
    {
        private static List<DataConnection> _connections { get; set; }

        public static void AddToPool(SqlConnection data, string connectionString, string transaction)
        {
            if (_connections == null)
            {
                _connections = new List<DataConnection>();
            }
            
            DataConnection dc = new DataConnection();
            dc.transaction = transaction;
            dc.lastAccess = DateTime.Now;
            dc.connection = data;
            dc.connectionString = connectionString;
            _connections.Add(dc);
        }

        public static void RemoveFromPool(string connectionString, string transaction)
        {
            for (int i = 0; i < _connections.Count; i++)
			{
                if (_connections[i].connectionString == connectionString && _connections[i].transaction == transaction)
                {
                    _connections[i].connection.Close();
                    _connections[i].connection.Dispose();
                    _connections.RemoveAt(i);
                    break;
                }
            }
        }

        public static void ClearInactiveConnections()
        {
            //for now no clearance
            return;
        }

        public static SqlConnection FindInPool(string connectionString, string transaction)
        {
            if (_connections == null)
            {
                _connections = new List<DataConnection>();
            }

            foreach (DataConnection con in _connections)
            {
                if (con.connectionString == connectionString && con.transaction == transaction)
                {
                    con.lastAccess = DateTime.Now;
                    if (con.connection.State != System.Data.ConnectionState.Open)
                    {
                        try
                        {
                            con.connection.ConnectionString = connectionString;
                            con.connection.Open();
                        }
                        catch (Exception)
                        {
                            con.connection.Close();
                            con.connection.Dispose();
                            con.connectionString = "";
                            return null;
                        }
                    }
                    return con.connection;
                }
            }
            return null;
        }
    }
}
