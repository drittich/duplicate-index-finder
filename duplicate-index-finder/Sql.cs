using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;

namespace duplicate_index_finder
{
    class Sql
	{
		public static IDbConnection GetConnection()
		{
			return GetConnection("main");
		}

		public static IDbConnection GetConnection(string connectionName)
		{
			var connectionStringSetting = ConfigurationManager.ConnectionStrings[connectionName];
			if (connectionStringSetting == null)
				throw new Exception(string.Format("Unable to find connection string [{0}] in config", connectionName));
			string connectionString = connectionStringSetting.ConnectionString;
			var cn = new SqlConnection(connectionString);
			cn.Open();
			return cn;
		}
	}
}
