using Discord;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.Classes
{
    public class WebUser : IUser
    {
        public ulong Id { get; set; }
        public string Username { get; set; }
        public ushort Discriminator { get; set; }

        public string AvatarId => throw new NotImplementedException();

        public ushort DiscriminatorValue => throw new NotImplementedException();

        public bool IsBot => throw new NotImplementedException();

        public bool IsWebhook => throw new NotImplementedException();

        public UserProperties? PublicFlags => throw new NotImplementedException();

        public DateTimeOffset CreatedAt => throw new NotImplementedException();

        public string Mention => throw new NotImplementedException();

        public UserStatus Status => throw new NotImplementedException();

        public IImmutableSet<ClientType> ActiveClients => throw new NotImplementedException();

        public IImmutableList<IActivity> Activities => throw new NotImplementedException();

        public string BannerId => throw new NotImplementedException();

        public Color? AccentColor => throw new NotImplementedException();

        string IUser.Discriminator => throw new NotImplementedException();

        public Task<IDMChannel> CreateDMChannelAsync(RequestOptions options = null)
        {
            throw new NotImplementedException();
        }

        public string GetAvatarUrl(ImageFormat format = ImageFormat.Auto, ushort size = 128)
        {
            throw new NotImplementedException();
        }

        public string GetBannerUrl(ImageFormat format = ImageFormat.Auto, ushort size = 256)
        {
            throw new NotImplementedException();
        }

        public string GetDefaultAvatarUrl()
        {
            throw new NotImplementedException();
        }
    }
}
