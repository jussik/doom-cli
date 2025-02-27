using DoomCli;
using Spectre.Console.Cli;

var app = new CommandApp();
app.SetDefaultCommand<ShortcutCommand>();
app.Configure(cfg =>
{
    cfg.AddCommand<ShortcutCommand>("shortcut");
});
return app.Run(args);