using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SendEmailLambdaSQS
{
    public class EmailMessage
    {
        public string Email { get; set; }
        public string EventType { get; set; }
        public string Language { get; set; }
        public Dictionary<string, string> Placeholders { get; set; }
    }

    public class EmailTemplate
    {
        public string Subject { get; set; }
        public string Body { get; set; }
    }

}
