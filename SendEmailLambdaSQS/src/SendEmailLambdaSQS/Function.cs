using System.Net.Mail;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SendEmailLambdaSQS;

public class Function
{

    private readonly string _connStr = Environment.GetEnvironmentVariable("DB_CONNECTION")!;
    private readonly string _smtpUsername = Environment.GetEnvironmentVariable("SMTP_USERNAME")!;
    private readonly string _smtpPassword = Environment.GetEnvironmentVariable("SMTP_PASSWORD")!;
    private readonly SqlConnection _conn;
    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        _conn = new SqlConnection(_connStr);
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        context.Logger.LogLine(evnt.Records.Count().ToString());
        foreach (var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        try
        {
            context.Logger.LogLine($"Processing message ID: {message.MessageId}");
            var messageBody = JsonConvert.DeserializeObject<EmailMessage>(message.Body)!;
            context.Logger.LogLine($"Processing message ID: {messageBody.EventType} , {messageBody.Language}");
            var template = await GetTemplate(messageBody.EventType, messageBody.Language);
            context.Logger.LogLine($"Processing message ID: {template == null}");
            var body = ReplacePlaceholders(template.Body, messageBody.Placeholders);
            context.Logger.LogLine($"Processing message ID: {template.Body}");
            await SendEmail(messageBody.Email, template.Subject, body);
            context.Logger.LogLine($"Processing message ID: Send");
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error: {ex.InnerException?.StackTrace}");
        }
    }

    private async Task<EmailTemplate> GetTemplate(string eventType, string language)
    {
        if(_conn.State != System.Data.ConnectionState.Open)
            await _conn.OpenAsync();

        var sql = "SELECT Subject, Body FROM EmailTemplates WHERE EventType = @eventType AND Language = @language";
        using var cmd = new SqlCommand(sql, _conn);
        cmd.Parameters.AddWithValue("@eventType", eventType);
        cmd.Parameters.AddWithValue("@language", language);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new EmailTemplate
            {
                Subject = reader.GetString(reader.GetOrdinal("Subject")),
                Body = reader.GetString(reader.GetOrdinal("Body"))
            };
        }
        else
        {
            throw new Exception("Email template not found.");
        }
    }

    private string ReplacePlaceholders(string body, Dictionary<string, string> placeholders)
    {
        foreach (var kvp in placeholders)
        {
            body = body.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        }
        return body;
    }

    private async Task SendEmail(string to, string subject, string body)
    {
        using var smtp = new SmtpClient("smtp.gmail.com", 587)
        {
            Credentials = new System.Net.NetworkCredential(_smtpUsername, _smtpPassword),
            EnableSsl = true
        };

        var mail = new MailMessage(_smtpUsername, to, subject, body)
        {
            IsBodyHtml = true
        };

        await smtp.SendMailAsync(mail);
    }

}