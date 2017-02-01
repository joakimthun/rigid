﻿using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Rigid.Exceptions;

namespace Rigid.Tests
{
    [TestFixture]
    public class MatcherTests : RequestTestsBase
    {
        [Test]
        public void Type_matchers_matches_the_property_if_match_returns_true()
        {
            Rigid.Get("https://www.test.com", () => CreateMockedJsonHttpClient(new
                {
                    TestStr = "123",
                    TestInt = 123,
                    TestFloat = 1.23f,
                    TestRegex = "Hello!",
                    TestArray = new [] { 1 ,2, 3 },
                    TestObject = new { Prop = 123 }
                }))
                .AssertJson(new
                    {
                        TestStr = Rigid.Matchers.String,
                        TestInt = Rigid.Matchers.Int,
                        TestFloat = Rigid.Matchers.Float,
                        TestRegex = Rigid.Matchers.Regex(new Regex("^He")),
                        TestArray = Rigid.Matchers.Array,
                        TestObject = Rigid.Matchers.Object
                    })
                .Execute();
        }

        [Test]
        public void Type_matchers_generates_a_correct_error_message_if_match_returns_false()
        {
            var exception = Assert.Catch<AssertFailedException>(() =>
            {
                Rigid.Get("https://www.test.com", () => CreateMockedJsonHttpClient(new
                {
                    TestStr = "123",
                    TestInt = 123,
                    TestFloat = 1.23f,
                    TestRegex = "Hello!",
                    TestArray = new[] { 1, 2, 3 },
                    TestObject = new { Prop = 123 }
                }))
                .AssertJson(new
                {
                    TestStr = Rigid.Matchers.Int,
                    TestInt = Rigid.Matchers.String,
                    TestFloat = Rigid.Matchers.Regex(new Regex("^He")),
                    TestRegex = Rigid.Matchers.Float,
                    TestArray = Rigid.Matchers.Object,
                    TestObject = Rigid.Matchers.Array
                })
                .Execute();
            });

            Assert.IsTrue(exception.FailedResults.Single().Message.Contains("The property 'TestStr' did not match the specified matcher. Message: Type mismatch. Expected: 'Integer', Actual: 'String'."));
            Assert.IsTrue(exception.FailedResults.Single().Message.Contains("The property 'TestInt' did not match the specified matcher. Message: Type mismatch. Expected: 'String', Actual: 'Integer'."));
            Assert.IsTrue(exception.FailedResults.Single().Message.Contains("The property 'TestFloat' did not match the specified matcher. Message: The RegexMatcher did not match the actual value: '1.23'."));
            Assert.IsTrue(exception.FailedResults.Single().Message.Contains("The property 'TestRegex' did not match the specified matcher. Message: Type mismatch. Expected: 'Float', Actual: 'String'."));
            Assert.IsTrue(exception.FailedResults.Single().Message.Contains("The property 'TestArray' did not match the specified matcher. Message: Type mismatch. Expected: 'Object', Actual: 'Array'."));
            Assert.IsTrue(exception.FailedResults.Single().Message.Contains("The property 'TestObject' did not match the specified matcher. Message: Type mismatch. Expected: 'Array', Actual: 'Object'."));
        }
    }
}