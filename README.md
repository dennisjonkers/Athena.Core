# Athena.Core
This project creates a connectivity with a database. Currently aimed to support MSSQL, MySQL and Orcacle. This core functionality for connecting to databases in a general way is used by athena online, megatec internet solutions and the athena innovation group

Library
=============

This library can be used widely and in many applications. Some features:

1. Querybuilder to fetch queries from a database into a DataTable. 

```C#
QueryBuilder qb = new QueryBuilder();
qb.AppendSelect("ID");
qb.AppendFrom("Customers");
DataTable dt = qb.GetData();

//then use following to get the first ID
dt.Rows[0][0]
```
2. UpdateBuilder to update a record
```C#
UpdateBuilder ub = new UpdateBuilder();
ub.Table = "Customer"
ub.AppendValue("Name", "Test");
ub.AppendWhere("ID", 1);
ub.Execute();

//Or get back the identity inserted
int iLastRecord = ub.ExecuteIdentity();
```
0. Deletebuilder to delete records
0. InsertBuilder to insert records
0. All is being taken care off depending on app.settings which provides the environment, once you have set up an Environment you can use above features

Please see our [contributing guidelines](CONTRIBUTING.md) before reporting an issue.
