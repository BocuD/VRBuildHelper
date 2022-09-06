using System;
using System.Collections.Generic;
using BocuD.BuildHelper;
using BocuD.VRChatApiTools;
using UnityEngine;
using VRC.Core;

namespace BuildHelper.Runtime
{
    
#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public class DiscordWebhookTools
    {
        [Serializable]
        public class DiscordWebhookData
        {
            public string url = "";
            public string username = "VRBuildHelper";
            public string avatarUrl = "";
            
            public string message = "{WorldName} was just updated!";

            public Color color;

            public EmbedMode embedMode = EmbedMode.Embed;
            public enum EmbedMode
            {
                Embed,
                NoEmbed
            }
        }
        
        public static async void SendPublishedMessage(Branch branch)
        {
            DiscordWebhookData data = branch.webhookSettings;
            ApiWorld worldData = await VRChatApiTools.FetchApiWorldAsync(branch.blueprintID);

            DiscordMessage message = new DiscordMessage
            {
                username = data.username,
                avatar_url = data.avatarUrl,
            };

            switch (data.embedMode)
            {
                case DiscordWebhookData.EmbedMode.Embed:
                    DiscordMessage.Embed embed = new DiscordMessage.Embed
                    {
                        title = branch.cachedName,
                        description = data.message.Replace("{WorldName}", worldData.name).Replace("{BranchName}", branch.name),
                        url = "https://vrchat.com/home/launch?worldId=" + branch.blueprintID,
                        color = (int) data.color.r * 255 << 16 | (int) data.color.g * 255 << 8 | (int) data.color.b * 255,

                        thumbnail = new DiscordMessage.Embed.Thumbnail
                        {
                            url = worldData.thumbnailImageUrl
                        },
                        
                        fields = new List<DiscordMessage.Embed.Field>
                        {
                            new DiscordMessage.Embed.Field { name = "Branch", value = branch.name, inline = true },
                            new DiscordMessage.Embed.Field { name = "Version", value = worldData.version.ToString(), inline = true },
                        },
                    };
                    
                    message.embeds.Add(embed);
                    break;
                
                case DiscordWebhookData.EmbedMode.NoEmbed:
                    message.content = data.message;
                    break;
            }

            message.SendMessage(data.url);
        }
    }
#endif
}