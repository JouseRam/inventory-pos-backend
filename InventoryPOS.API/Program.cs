using InventoryPOS.API;

var app = PosWebApp.Build(new WebApplicationOptions { Args = args });
await PosWebApp.InitializeAsync(app);
app.Run();

public partial class Program;
