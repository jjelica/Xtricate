﻿using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Ploeh.AutoFixture;
using StackExchange.Profiling;
using Xtricate.DocSet;
using Xtricate.Dynamic;
using Xtricate.UnitTests.TestHelpers;

namespace Xtricate.IntegrationTests
{
    [TestFixture]
    public class DocStorageTests
    {
        [TestFixtureSetUp]
        public void Setup()
        {
            MiniProfiler.Settings.Storage = new MiniPofilerInMemoryStorage();
            MiniProfiler.Settings.ProfilerProvider = new MiniPofilerInMemoryProvider();
        }

        [Test]
        public void InitializeTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            storage.Initialize();
            storage.Reset();

            Assert.That(storage.Count(), Is.EqualTo(0));
        }

        [Test]
        public void FindTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            MiniProfiler.Start();
            var mp = MiniProfiler.Current;

            Trace.WriteLine($"pre count: {storage.Count(new[] { "en-US" })}");
            var key = DateTime.Now.Epoch() + new Random().Next(10000, 99999) + "c";
            var name = "NEWNAME" + key;
            var sku = "";
            using (mp.Step("insert "))
            {
                var document = new Fixture().Create<TestDocument>();
                document.Name = name;
                sku = document.Skus.FirstOrDefault().Sku;
                dynamic dDocument = document;
                dDocument.Dyn = "dynamic property";
                var result1 = storage.Upsert(key, document, new[] { "en-US" });
                Assert.That(result1, Is.EqualTo(StorageAction.Inserted));
                Trace.WriteLine("newDoc: " + document.Name);
            }

            5.Times(i =>
            {
                using (mp.Step("find by KEY/tags " + i))
                {
                    var result = storage.Load(key, new[] {"en-US"}).ToList();
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result.Any(), Is.True);
                    Assert.That(result.FirstOrDefault().Name, Is.EqualTo(name));
                }
            });

            5.Times(i =>
            {
                using (mp.Step("find by NAME criteria/tags " + i))
                {
                    var criterias = new List<Criteria> {new Criteria("name", CriteriaOperator.Eq, name)};
                    var result = storage.Load(new[] { "en-US" }, criterias).ToList();
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result.Any(), Is.True);
                    Assert.That(result.FirstOrDefault().Name, Is.EqualTo(name));
                }
            });

            5.Times(i =>
            {
                using (mp.Step("find by SKU criteria/tags " + i))
                {
                    var criterias = new List<Criteria> { new Criteria("sku", CriteriaOperator.Contains, sku) };
                    var result = storage.Load(new[] { "en-US" }, criterias).ToList();
                    Assert.That(result, Is.Not.Null);
                    Assert.That(result.Any(), Is.True);
                    Assert.That(result.FirstOrDefault().Skus.FirstOrDefault().Sku, Is.EqualTo(sku));
                }
            });

            Trace.WriteLine($"trace: {mp.RenderPlainText()}");
            MiniProfiler.Stop();
        }

        [TestCase(1000, false)]
        public void MassInsertTest(int docCount, bool reset)
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            MiniProfiler.Start();
            var mp = MiniProfiler.Current;

            if(reset) storage.Reset();
            Trace.WriteLine($"pre count: {storage.Count(new[] { "en-US" })}");

            var key = DateTime.Now.Epoch() + new Random().Next(10000, 99999);
            for (var i = 1; i <= docCount; i++)
            {
                Trace.WriteLine($"+{i}");
                storage.Upsert(key + i, new Fixture().Create<TestDocument>(), new[] {"en-US"});
            }

            Trace.WriteLine($"post count: {storage.Count(new[] { "en-US" })}");
            Trace.WriteLine($"trace: {mp.RenderPlainText()}");
            MiniProfiler.Stop();
        }

        [Test]
        public void InsertTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            MiniProfiler.Start();
            var mp = MiniProfiler.Current;

            storage.Reset();
            var preCount = storage.Count(new[] {"en-US"});
            Trace.WriteLine($"pre count: {preCount}");

            var key = DateTime.Now.Epoch() + new Random().Next(10000, 99999);
            for (var i = 1; i < 100; i++)
            {
                Trace.WriteLine($"+{i}");
                using (mp.Step("insert " + i))
                {
                    var result1 = storage.Upsert("key1", new Fixture().Create<TestDocument>(), new[] {"en-US"});
                //    Assert.That(result1, Is.EqualTo(StorageAction.Updated));
                //}
                //using (mp.Step("upsert string"))
                //{
                    var result2 = storage.Upsert(Guid.NewGuid(), new Fixture().Create<TestDocument>(), new[] {"en-US"});
                    //Assert.That(result2, Is.EqualTo(StorageAction.Inserted));
                //}
                //using (mp.Step("upsert int"))
                //{
                    var result3 = storage.Upsert(key + i, new Fixture().Create<TestDocument>(), new[] {"en-US"});
                    //Assert.That(result3, Is.EqualTo(StorageAction.Inserted));
                }
            }

            for (var i = 1; i <= 5; i++)
            {
                using (mp.Step("load " + i))
                {
                    var result = storage.Load(new[] {"en-US"}).Take(100);
                    //Assert.That(result, Is.Not.Null);
                    //Assert.That(result, Is.Not.Empty);
                    Trace.WriteLine($"loaded count: {result.Count()}");
                    Trace.WriteLine($"first: {result.FirstOrDefault().Id}");
                    //result.ForEach(r => Trace.Write(r.Id));
                    //result.ForEach(r => Assert.That(r, Is.Not.Null));
                    result.ForEach(r => Trace.WriteLine(r, r.Name));
                }
            }

            using (mp.Step("post count"))
            {
                var postCount = storage.Count(new[] {"en-US"});
                Trace.WriteLine($"post count: {postCount}");
                //Assert.That(storage.Count(), Is.GreaterThan(preCount));
            }
            Trace.WriteLine($"trace: {mp.RenderPlainText()}");
            MiniProfiler.Stop();
        }

        [Test]
        public void UpdateTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            MiniProfiler.Start();
            var mp = MiniProfiler.Current;

            storage.Reset();

            var key = DateTime.Now.Epoch() + new Random().Next(10000, 99999);
            using (mp.Step("insert "))
            {
                var newDoc = new Fixture().Create<TestDocument>();
                var result1 = storage.Upsert(key, newDoc, new[] {"en-US"});
                Assert.That(result1, Is.EqualTo(StorageAction.Inserted));
                Trace.WriteLine("newDoc: " + newDoc.Name);

                newDoc.Name = Guid.NewGuid().ToString();
                var result2 = storage.Upsert(key, newDoc, new[] { "en-US" });
                Assert.That(result2, Is.EqualTo(StorageAction.Updated));
                Trace.WriteLine("newDoc: " + newDoc.Name);

                var updatedDoc = storage.Load(key, new[] {"en-US"}).ToList();
                Assert.That(updatedDoc, Is.Not.Null);
                Assert.That(updatedDoc.Any(), Is.True);
                Assert.That(updatedDoc.Count(), Is.EqualTo(1));
                Assert.That(updatedDoc.First().Name, Is.Not.Null);
                Assert.That(updatedDoc.First().Name, Is.EqualTo(newDoc.Name));
                Trace.WriteLine("updatedDoc: " + updatedDoc.First().Name);
            }
            Trace.WriteLine($"trace: {mp.RenderPlainText()}");
            MiniProfiler.Stop();
        }

        [Test]
        public void DeleteTest()
        {
            var options = new StorageOptions("TestDb", "StorageTests");
            var connectionFactory = new SqlConnectionFactory();
            var indexMap = TestDocumentIndexMap;
            var storage = new DocStorage<TestDocument>(connectionFactory, options, new SqlBuilder(),
                new JsonNetSerializer(), new Md5Hasher(), indexMap);

            storage.Initialize();
            storage.Reset();

            Assert.That(storage.Count(), Is.EqualTo(0));

            var doc1 = new Fixture().Create<TestDocument>();
            storage.Upsert("key1", doc1, new[] {"en-US"});

            var doc2 = new Fixture().Create<TestDocument>();
            storage.Upsert("key2", doc1, new[] {"en-US"});

            var doc3 = new Fixture().Create<TestDocument>();
            storage.Upsert("key1", doc1, new[] {"en-GB"});

            Assert.That(storage.Count(), Is.EqualTo(3));

            var result1 =storage.Delete("key1", new[] { "en-US" }); // removes only key1 + en-US

            Assert.That(result1, Is.EqualTo(StorageAction.Deleted));
            Assert.That(storage.Count(), Is.EqualTo(2));

            var result2 = storage.Delete("key1"); // removes all with key1

            Assert.That(result2, Is.EqualTo(StorageAction.Deleted));
            Assert.That(storage.Count(), Is.EqualTo(1));

            var result3 = storage.Delete("key2"); // removes all with key2

            Assert.That(result3, Is.EqualTo(StorageAction.Deleted));
            Assert.That(storage.Count(), Is.EqualTo(0));
        }

        private static List<IIndexMap<TestDocument>> TestDocumentIndexMap
        {
            get
            {
                return new List<IIndexMap<TestDocument>>
                {
                    new IndexMap<TestDocument>(nameof(TestDocument.Name), i => i.Name),
                    new IndexMap<TestDocument>(nameof(TestDocument.Group), i => i.Group),
                    new IndexMap<TestDocument>(nameof(TestSku.Sku), values: i => i.Skus.Select(s => s.Sku)),
                    new IndexMap<TestDocument>(nameof(TestDocument.Date), i =>
                        i.Date.HasValue ? i.Date.Value.ToString("s") : null)
                };
            }
        }
    }

    public class TestDocument : Expando
    {
        public int Id { get; set; }
        public IDictionary<string, string> Identifiers { get; set; }
        public string Name { get; set; }
        public string ShortDescription { get; set; }
        public string LongDescription { get; set; }
        public string Group { get; set; }
        public int Position { get; set; }
        public IEnumerable<string> MetaKeywords { get; set; }
        public string MetaDescription { get; set; }
        public TestEnum State { get; set; }
        public DateTime? Date { get; set; }
        public IEnumerable<TestSku> Skus { get; set; }
        public IEnumerable<TestAttributeValue> Features { get; set; }
        public IEnumerable<TestAttributeValue> Relations { get; set; }
        public IEnumerable<TestAttributeValue> Includes { get; set; }
        public IEnumerable<TestAttributeValue> Attributes { get; set; }
    }

    public class TestSku
    {
        public string Sku { get; set; }
        public string Cso { get; set; }
        public string Gtin { get; set; }
        public string Ean { get; set; }
        public string Upc { get; set; }
    }

    public class TestAttributeValue
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Position { get; set; }
        public string TextValue { get; set; }
        public int? MediaValue { get; set; }
        public decimal? NumberValue { get; set; }
        public bool? BooleanValue { get; set; }
        public int? CategoryValue { get; set; }
        public int? ProductValue { get; set; }
        public DateTime? DateValue { get; set; }
    }

    public enum TestEnum
    {
        Open,
        Closed
    }
}