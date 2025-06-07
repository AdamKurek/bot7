using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SpotifyAPI.Web.Http;
using System.Text.Json;
using LMStudioNET.Objects.Chat;


namespace bot7
{
    public class Message
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    public class ChatCompletionRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public List<Message> Messages { get; set; }

        [JsonProperty("temperature")]
        public double Temperature { get; set; } = 0.7;
    }

    public class LmStudioCaller
    {
        private readonly HttpClient client = new HttpClient();

        public async Task<string> call(string message)
        {
            var request = new ChatCompletionRequest
            {
                Model = "your_model_identifier_here", // Replace with your actual model ID from LM Studio
                Messages = new List<Message>
            {
                new Message
                {
                    Role = "user",
                    Content = message
                }
            },
                Temperature = 0.7
            };




            string json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");


            try {
                HttpResponseMessage response = await client.PostAsync("http://192.168.0.21:1234/v1/chat/completions", content);
                var bytes = await response.Content.ReadAsStringAsync();

                var rsp = JsonConvert.DeserializeObject<ChatCompletionResponse>(bytes);
                return rsp.Choices[0].Message.Content;

            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return e.Message;
            }

        }
    }
}
