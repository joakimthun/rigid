﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Claim.Extensions;
using Claim.ValueMatchers;

namespace Claim.Asserts
{
    public enum PropertyComparison : byte
    {
        IgnoreCase
    }

    public class JsonAssert : IAssert
    {
        private readonly object _expectedResponseStructure;
        private readonly PropertyComparison? _propertyComparison;
        private readonly ICollection<string> _errors = new List<string>();
        private readonly Stack<string> _expectedPropertyPath = new Stack<string>();

        public JsonAssert(object expectedResponseStructure, PropertyComparison? propertyComparison = null)
        {
            _expectedResponseStructure = expectedResponseStructure;
            _propertyComparison = propertyComparison;
        }

        public JsonAssert(string expectedResponseStructure, PropertyComparison? propertyComparison = null)
        {
            _expectedResponseStructure = JsonConvert.DeserializeObject(expectedResponseStructure);
            _propertyComparison = propertyComparison;
        }

        public static IEnumerable<string> CompareArrays(PropertyInfo expectedProperty, object expected, JToken actual, bool matchLength, bool checkPropertyType)
        {
            var assert = new JsonAssert(expected);

            assert.PushExpectedPropertyPath(expectedProperty.Name);
            assert.CompareArraysInternal(expectedProperty, expected, actual, matchLength, checkPropertyType);
            assert.PopExpectedPropertyPath();

            return assert._errors;
        }

        public Result Assert(Response response)
        {
            if(response.ResponseContent == null || response.ResponseContent.Length == 0)
                return Result.Failed<JsonAssert>("JsonAssert: Empty response body.");

            try
            {
                Verify(_expectedResponseStructure, JObject.Parse(Encoding.UTF8.GetString(response.ResponseContent, 0, response.ResponseContent.Length)));
            }
            catch (JsonReaderException)
            {
                var actualContentType = response.ResponseMessage?.Content?.Headers?.ContentType?.ToString() ?? "Unknown";
                return Result.Failed<JsonAssert>($"JsonAssert: Not a valid json response. Actual content type: '{actualContentType}'");
            }

            if (_errors.Any())
                return Result.Failed<JsonAssert>(_errors.ToNewLineSeparatedList());

            return Result.Passed<JsonAssert>();
        }

        private void Verify(object expected, JObject actual)
        {
            foreach (var property in expected.GetProperties())
            {
                PushExpectedPropertyPath(property.Name);
                VerifyProperty(property, expected, actual);
                PopExpectedPropertyPath();
            }
        }

        private void VerifyProperty(PropertyInfo expectedProperty, object expected, JObject actual)
        {
            JProperty actualProperty;
            if (_propertyComparison == PropertyComparison.IgnoreCase)
            {
                actualProperty = actual.Properties().SingleOrDefault(x => string.Equals(x.Name, expectedProperty.Name, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                actualProperty = actual.Properties().SingleOrDefault(x => x.Name == expectedProperty.Name);
            }

            if (actualProperty == null)
            {
                _errors.Add($"The expected property '{GetExpectedPropertyPathName(expectedProperty)}' was not present in the response.");
                return;
            }

            var expectedPropertyValue = expectedProperty.GetValue(expected);
            VerifyProperty(expectedProperty, expectedPropertyValue, actualProperty.Value);
        }

        private void VerifyProperty(PropertyInfo expectedProperty, object expected, JToken actual)
        {
            if (expectedProperty.PropertyType.ImplementsInterface<IPropertyValueMatcher>())
            {
                var matcher = (IPropertyValueMatcher)expected;
                var matchingResult = matcher.Match(expectedProperty, actual);
                if (!matchingResult.Success)
                {
                    matchingResult.Messages.ForEach(msg => _errors.Add($"The property '{GetExpectedPropertyPathName(expectedProperty)}' did not match the specified matcher. Message: {msg ?? string.Empty}"));
                }

                return;
            }

            CompareTypeAndValue(expectedProperty, expected, actual);
        }

        private void CompareTypeAndValue(PropertyInfo expectedProperty, object expected, JToken actual)
        {
            if (actual.Type == JTokenType.Object)
            {
                Verify(expected, (JObject)actual);
                return;
            }
            if (actual.Type == JTokenType.Array)
            {
                CompareArraysInternal(expectedProperty, expected, actual);
                return;
            }

            try
            {
                var actualValue = actual.CastToSystemType(expected.GetType());

                if (!expected.Equals(actualValue))
                    _errors.Add($"The expected property '{GetExpectedPropertyPathName(expectedProperty)}' does not have the same value as the property in the response. Expected value: '{expected}'. Actual value: '{actualValue}'");
            }
            catch (InvalidCastException)
            {
                AddWrongTypeError(expectedProperty, actual);
            }
        }

        private void CompareArraysInternal(PropertyInfo expectedProperty, object expected, JToken actual, bool matchLength = true, bool checkPropertyType = true)
        {
            if (checkPropertyType && !expectedProperty.PropertyType.IsArray)
            {
                AddWrongTypeError(expectedProperty, actual);
                return;
            }

            var expectedArray = (Array)expected;

            if (matchLength && (expectedArray.Length != actual.Children().Count()))
            {
                _errors.Add($"The expected array property '{GetExpectedPropertyPathName(expectedProperty)}' is not of the same length as the array in the response. Expected length: '{expectedArray.Length}'. Actual length: '{actual.Children().Count()}'");
                return;
            }

            for (var i = 0; i < expectedArray.Length; i++)
            {
                var actualElement = actual.Children().ElementAtOrDefault(i);
                if(actualElement == null)
                    break;

                PushExpectedPropertyPath($"[{i}]");

                var expectedElement = expectedArray.GetValue(i);
                CompareTypeAndValue(expectedProperty, expectedElement, actualElement);

                PopExpectedPropertyPath();
            }
        }

        private void AddWrongTypeError(PropertyInfo expectedProperty, JToken actual)
        {
            _errors.Add($"The expected property '{GetExpectedPropertyPathName(expectedProperty)}' is not of the same type as the property in the response. Expected type: '{GetExpectedPropertyTypeName(expectedProperty)}'. Actual type: '{actual.Type}'");
        }

        private string GetExpectedPropertyTypeName(PropertyInfo expectedProperty)
        {
            var type = expectedProperty.PropertyType;

            if (expectedProperty.PropertyType.IsArray)
                type = expectedProperty.PropertyType.GetElementType();

            if (type.Name.Contains("AnonymousType"))
                return "Object";

            return type.Name;
        }

        private string GetExpectedPropertyPathName(PropertyInfo currentLevelExpectedProperty)
        {
            if (!_expectedPropertyPath.Any())
                return currentLevelExpectedProperty.Name;

            return _expectedPropertyPath.Reverse().JsonObjectPathJoin();
        }

        private void PushExpectedPropertyPath(string value)
        {
            _expectedPropertyPath.Push(value);
        }

        private void PopExpectedPropertyPath()
        {
            _expectedPropertyPath.Pop();
        }
    }
}
