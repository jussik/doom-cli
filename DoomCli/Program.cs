using DoomCli.Configure;
using DoomCli.Shortcut;
using Spectre.Console.Cli;

var app = new CommandApp();
app.SetDefaultCommand<ShortcutCommand>()
    .WithDescription("Create a Doom shortcut");
app.Configure(cfg =>
{
    cfg.AddCommand<ConfigureCommand>("configure")
        .WithDescription("Configure DoomCli");
});
return app.Run(args);