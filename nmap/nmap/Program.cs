using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .SetMinimumLevel(LogLevel.Trace)
        .AddConsole();
});
var logger = loggerFactory.CreateLogger<Program>();

var apiKey = configuration.GetSection("API_KEY").Value;
var botClient = new TelegramBotClient(apiKey);

var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = { } // receive all update types
};

var cancellationTokenSource = new CancellationTokenSource();
var cancellationToken = cancellationTokenSource.Token;

var updateReceiver = new QueuedUpdateReceiver(botClient, receiverOptions);

await foreach (var update in updateReceiver.WithCancellation(cancellationToken))
{
    // skip all not valid updates from bot
    if (update.Message is not { } message
        || update.Message.From is { IsBot: true }
        || update.Message.Text is null or "")
        continue;
    
    try
    {
        logger.LogInformation("Message raw text \'{MessageText}\'", message.Text);

        var arguments = message.Text!.Trim();

        await Say(botClient, message.Chat, $"Start processing '{arguments}'", cancellationToken);

        // run task with 'nmap' process
        Task.Run(async () =>
        {
            // start 'nmap' process
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"nmap {arguments}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.Start();

            var output = new StringBuilder();

            output.AppendLine($"Response for '{arguments}':");
            output.AppendLine(process.StandardOutput.ReadToEnd());
            output.AppendLine(process.StandardError.ReadToEnd());

            process.WaitForExit();

            await Say(botClient, message.Chat, output.ToString(), cancellationToken);
        }, new CancellationTokenSource(TimeSpan.FromMinutes(30)).Token);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An exception was caught");
        await Say(botClient, message.Chat, $"An exception was caught with the message '{ex.Message}'", cancellationToken);
    }
}

async Task Say(ITelegramBotClient client, Chat chat, string text, CancellationToken token)
{
    await client.SendChatActionAsync(chat, ChatAction.Typing, cancellationToken: token);

    // text limit size per message is 4096 symbols, so split it into multiple messages
    foreach (var message in text.Chunk(4000).Select(value => new string(value)))
    {
        await client.SendTextMessageAsync(chat, message, cancellationToken: token);
    }
}