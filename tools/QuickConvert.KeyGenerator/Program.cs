using System.Text.Json;
using QuickConvert.Core.Messaging;

Console.WriteLine(JsonSerializer.Serialize(
    ChromeExtensionIdentity.Generate(),
    new JsonSerializerOptions { WriteIndented = true }));
