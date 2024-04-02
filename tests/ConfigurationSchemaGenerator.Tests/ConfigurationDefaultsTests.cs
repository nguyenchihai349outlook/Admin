// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;
using Xunit;

namespace ConfigurationSchemaGenerator.Tests;

public class ConfigurationDefaultsTests
{
    [Fact]
    public void ShouldMergeNewProperties()
    {
        var target = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["Aspire"] = new JsonObject
                {
                    ["type"] = "object"
                }
            }
        };

        var source = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["Aspire"] = new JsonObject
                {
                    ["description"] = "This is a description"
                }
            }
        };

        ConfigSchemaEmitter.MergeProperties(target, source);

        Assert.Equal("This is a description", target["properties"]?["Aspire"]?["description"]?.ToString());
    }

    [Fact]
    public void ShouldNotMergeExistingProperties()
    {
        var target = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["Aspire"] = new JsonObject
                {
                    ["type"] = "object"
                }
            }
        };

        var source = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["Aspire"] = new JsonObject
                {
                    ["type"] = "This is a new type"
                }
            }
        };

        ConfigSchemaEmitter.MergeProperties(target, source);

        Assert.Equal("object", target["properties"]?["Aspire"]?["type"]?.ToString());
    }

    [Fact]
    public void ShouldNotMergeNewObject()
    {
        var target = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["Aspire"] = new JsonObject
                {
                    ["type"] = "object"
                }
            }
        };

        var source = new JsonObject
        {
            ["properties"] = new JsonObject
            {
                ["Aspire"] = new JsonObject
                {
                    ["description"] = new JsonObject
                    {
                        ["type"] = "This is a new object"
                    }
                }
            }
        };

        ConfigSchemaEmitter.MergeProperties(target, source);

        Assert.Null(target["properties"]?["Aspire"]?["description"]);
    }
}
