using System;
using System.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace Athena.Core.Test
{
    [TestClass]
    public class QueryBuilderTest
    {

        [TestCategory("Nightly"), TestMethod()]
        public void TransactionTest()
        {

            QueryBuilder qb = new QueryBuilder();
            qb.AppendSelect("ID, Name");
            qb.AppendFrom("Customers");
        }

        [TestCategory("Nightly"), TestMethod()]
        public void MapperTest()
        {
            QueryBuilder qb = new QueryBuilder();
            qb.AppendSelect("ID, Name, SysCreated");
            qb.AppendFrom("Customers");
            for (int i = 0; i < 100; i++)
            {
                IList<Helpers.Customer> customers = qb.GetDataList<Helpers.Customer>();
            }

        }

        [TestCategory("Nightly"), TestMethod()]
        public void EncryptionTest()
        {
            string sTest = Encryption.Encrypt("test");
            sTest = Encryption.Decrypt(sTest);
            Assert.AreEqual(sTest, "test");

        }
    }
}
