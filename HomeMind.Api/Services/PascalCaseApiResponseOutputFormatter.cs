using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace HomeMind.Api.Services;

/// <summary>
/// 出参格式化器：仅对 ApiResponse&lt;T&gt; 类型的响应生效，把 Data 字段用 C# PascalCase
/// 序列化（大驼峰），其余响应沿用系统默认 JSON 行为。
/// </summary>
public sealed class PascalCaseApiResponseOutputFormatter : OutputFormatter
{
    private static readonly JsonSerializerOptions PascalCaseOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    public PascalCaseApiResponseOutputFormatter()
    {
        SupportedMediaTypes.Add("application/json");
        SupportedMediaTypes.Add("text/json");
    }

    protected override bool CanWriteType(Type? type) =>
        type is not null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ApiResponse<>);

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        var response = context.HttpContext.Response;
        var apiResponse = context.Object;
        if (apiResponse is null)
        {
            await response.Body.WriteAsync(Encoding.UTF8.GetBytes("null"));
            return;
        }

        var type = apiResponse.GetType();
        var code = (int)(type.GetProperty(nameof(ApiResponse<object>.Code))!.GetValue(apiResponse) ?? 0);
        var msg = (string)(type.GetProperty(nameof(ApiResponse<object>.Msg))!.GetValue(apiResponse) ?? string.Empty)!;
        var data = type.GetProperty(nameof(ApiResponse<object>.Data))!.GetValue(apiResponse);

        await using var buffer = new MemoryStream();
        await using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("Code", code);
            writer.WriteString("Msg", msg);
            if (data is null)
            {
                writer.WriteNull("Data");
            }
            else
            {
                writer.WritePropertyName("Data");
                JsonSerializer.Serialize(writer, data, data.GetType(), PascalCaseOptions);
            }
            writer.WriteEndObject();
        }

        var contentType = context.ContentType.HasValue ? context.ContentType.Value.ToString() : null;
        response.ContentType = string.IsNullOrEmpty(contentType) ? "application/json; charset=utf-8" : contentType;
        await response.Body.WriteAsync(buffer.ToArray());
    }
}
