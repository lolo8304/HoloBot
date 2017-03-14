#if WINDOWS_UWP
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using Newtonsoft.Json;
using Microsoft.Bot.Connector.DirectLine;


#endif

namespace MakerShowBotV3LibTestClient
{

#if WINDOWS_UWP
    
    // This block of code won't run in Unity's older version of Mono
    // This can only be run in a UWP device like the HoloLens
    public class Conversation
    {
        public string conversationId { get; set; }
        public string token { get; set; }
        public string eTag { get; set; }
        public string expires_in { get; set; }
    }

    public class ConversationReference
        {
            public string id { get; set; }
        }

      public class ConversationActitvities
    {
        public Activity[] activities { get; set; }
        public string watermark { get; set; }
        public string eTag { get; set; }
    }

    public class UserId
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    public class ActivityReference
    {
        public string id { get; set; }
    }

    public class Activity
    {
            public string id { get; set; }
            public ConversationReference conversation { get; set; }
            public UserId from { get; set; }
            public string type { get; set; }
            public string text { get; set; }
            public DateTime timestamp { get; set; }

            public string channelId { get; set; }
            public string replyToId { get; set; }
            public DateTime created { get; set; }
            public Channeldata channelData { get; set; }
            public string[] images { get; set; }
            public Attachment[] attachments { get; set; }
            public string eTag { get; set; }
    }

    public class Channeldata
    {
    }

    public class Attachment
    {
        public string url { get; set; }
        public string contentType { get; set; }
    }

    class KeyRequest
    {
        public string Mainkey { get; set; }
    }

    /*
     * explanations
     *      start conversation
     *          
     * 
     */



    /// <summary>
    /// The main service used to communicate with a Bot via the Bot Connector and the Direct Line Channel.
    /// This can only run in a UWP client.
    /// </summary>
    public class BotServiceV3Lib
    {
        // From the Bot Connector portal, enable the Direct Line channel on your bot
        // Generate and copy your Direct Line secret (aka API key)
        // TO DO: Please use your own key. This one connects to The Maker Show Bot
        private string _APIKEY = "JFMOFhh5p_Q.cwA.Of8.Uqu333UoxpTm_vHND3atQbgNDFMt4lF51Rqdl-1zfpc";
        private string botToken;
        private string activeConversation;
        private string activeWatermark;
        private string newActivityId;
        private string lastResponse;

        public BotServiceV3Lib()
        {
            // Constructor
        }

        public async Task<string> StartConversation()
        {
            //DirectLineClient client = new DirectLineClient(_APIKEY);
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri("https://directline.botframework.com/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Authorize
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _APIKEY);

                // Get a new token as dummy call
                var keyreq = new KeyRequest() { Mainkey = "" };
                var stringContent = new StringContent(keyreq.ToString());
                HttpResponseMessage response = await client.PostAsync("v3/directline/conversations", stringContent);
                //HttpResponseMessage response = await client.PostAsync("v3/directline/tokens/generate", stringContent);
                if (response.IsSuccessStatusCode)
                {
                    var re = response.Content.ReadAsStringAsync().Result;
                    var myConversation = JsonConvert.DeserializeObject<Conversation>(re);
                    activeConversation = myConversation.conversationId;
                    botToken = myConversation.token;
                    return myConversation.conversationId;
                }

            }
            return "Error";
        }

        public async Task<bool> SendMessage(string message)
        {
            using (var client = new HttpClient())
            {
                string conversationId = activeConversation;

                client.BaseAddress = new Uri("https://directline.botframework.com/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Authorize
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + botToken);

                // Send a message
                string messageId = Guid.NewGuid().ToString();
                DateTime timeStamp = DateTime.Now;
                var attachment = new Attachment();
                var myMessage = new Activity()
                {
                    type = "message",
                    from = new UserId() { id = "Joe" },
                    conversation = new ConversationReference { id = conversationId },
                    text = message
                };

                string postBody = JsonConvert.SerializeObject(myMessage);
                String urlString = "v3/directline/conversations/" + conversationId + "/activities";
                HttpContent httpContent = new StringContent(postBody, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(urlString, httpContent);
                if (response.IsSuccessStatusCode)
                {
                    var re = response.Content.ReadAsStringAsync().Result;
                    lastResponse = re;
                    var ar = JsonConvert.DeserializeObject<ActivityReference>(re);
                    newActivityId = ar.id;
                    return true;
                } else
                {
                    lastResponse = response.Content.ReadAsStringAsync().Result;
                }
            return false;
            }
        }

        public async Task<ConversationActitvities> GetNewestActivities()
        {
            Debug.WriteLine("searching for "+newActivityId);
            int inc = 0;
            ConversationActitvities cm = await GetMessages();
            while (++inc < 10) {
                Debug.WriteLine("activities size = " + cm.activities.Length);
                for (int i = 0; i < cm.activities.Length-1; i++)
                {
                    var activity = cm.activities[i];
                    lastResponse = activity.id + " / " + newActivityId;
                    if (activity.id.Equals(newActivityId)) {
                        return cm;
                    }
                }
                cm = await GetMessages();
            }
            return cm;
        }

        public async Task<ConversationActitvities> GetMessages()
        {
            using (var client = new HttpClient())
            {
                string conversationId = activeConversation;

                client.BaseAddress = new Uri("https://directline.botframework.com/");
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Authorize
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + botToken);

                ConversationActitvities cm = new ConversationActitvities();
                string messageURL = "v3/directline/conversations/" + conversationId + "/activities";
                if (activeWatermark != null)
                    messageURL += "?watermark=" + activeWatermark;
                HttpResponseMessage response = await client.GetAsync(messageURL);
                if (response.IsSuccessStatusCode)
                {
                    var re = response.Content.ReadAsStringAsync().Result;
                    lastResponse = re.ToString();
                    cm = JsonConvert.DeserializeObject<ConversationActitvities>(re);
                    activeWatermark = cm.watermark;
                    return cm;

                }
                return cm;
            }
        }
    }
#endif

#if !WINDOWS_UWP

    /// <summary>
    /// This is an empty shim for the BotService within Unity Mono, otherwise we'd get a
    /// compilation error in Unity when trying to instantiate this object.
    /// </summary>
    public class BotServiceV3Lib
    {

    }
#endif
}

