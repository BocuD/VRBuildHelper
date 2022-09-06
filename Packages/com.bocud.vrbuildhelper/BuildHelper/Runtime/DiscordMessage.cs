using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;

namespace BuildHelper.Runtime
{
    public class DiscordMessage
    {
        public string content;
        public string username;
        public string avatar_url;
        public List<Embed> embeds = new List<Embed>();

        public string SendMessage(string webhookURL)
        {
            string json = JsonConvert.SerializeObject(this);

            //create a new web request
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(webhookURL);
            request.ContentType = "application/json";
            request.Method = "POST";

            using (StreamWriter streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(json);
                streamWriter.Flush();
                streamWriter.Close();
            }

            string result;

            HttpWebResponse httpResponse = (HttpWebResponse)request.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                result = streamReader.ReadToEnd();
            }

            return result;
        }

        public class Embed
        {
            public string title;
            public string description;
            public string url;
            public int color;
            public Footer footer;
            public Image image;
            public Thumbnail thumbnail;
            public Author author;
            public List<Field> fields = new List<Field>();

            public class Footer
            {
                public string text;
                public string icon_url;
            }

            public class Image
            {
                public string url;
            }

            public class Thumbnail
            {
                public string url;
            }

            public class Author
            {
                public string name;
                public string url;
                public string icon_url;
            }

            public class Field
            {
                public string name;
                public string value;
                public bool inline;
            }
        }
    }
}