using Discord;
using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.MLAPI.Attributes;
using DiscordBot.Services;
using DiscordBot.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot.MLAPI.Modules
{

    public class FilterLists : AuthedAPIBase
    {
        const string FilterIdRegex = @"\w{8}-?\w{4}-?\w{4}-?\w{4}-?\w{12}";
        public FilterLists(APIContext context) : base(context, "filters")
        {
            Service = Context.Services.GetRequiredService<FilterListService>();
            DB = Context.Services.GetDb<FilterDbContext>("HTTP_" + context.Endpoint.Name);
        }
        public FilterListService Service { get; set; }
        public FilterDbContext DB { get; set; }

        [Method("GET"), Path("/filters")]
        public async Task ListFilters()
        {
            var filters = DB.GetFilters(Context.User.Id);

            var ul = new UnorderedList();
            await foreach(var filter in filters)
            {
                var anchor = new Anchor($"/filters/{filter.Id}");
                anchor.RawText = filter.Name;
                ul.AddItem(new ListItem() { Children = { anchor } });
            }
            var na = new Anchor($"#new", id: "new");
            na.RawText = "[Add new]";
            ul.AddItem(new ListItem() { Children = { na } });

            await ReplyFile("list.html", 200, new Replacements().Add("list", ul));
        }

        [Method("GET"), Path("/filters/new")]
        public async Task NewFilter()
        {
            await ReplyFile("edit.html", 200);
        }

        [Method("GET"), Path("/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public async Task EditFilter(Guid filterId)
        {
            await ReplyFile("edit.html", 200);
        }

        [Method("GET"), Path("/api/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public async Task APIGetFilter(Guid filterId)
        {
            var filter = await DB.GetFilter(filterId);
            if(filter == null)
            {
                await RespondRaw("Not found", 404);
                return;
            }
            var jobj = new JObject();
            jobj["id"] = filter.Id.ToString();
            jobj["name"] = filter.Name;
            jobj["text"] = filter.Text;
            jobj["template"] = filter.AutoAddTemplate;
            await RespondJson(jobj);
        }

        public struct PatchFilterData
        {
            public Optional<string> name { get; set; }
            public Optional<string> text { get; set; }
            public Optional<string> template { get; set; }
        }

        [Method("PATCH"), Path("/api/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public async Task APIEditFilter(Guid filterId, [FromBody]PatchFilterData data)
        {
            FilterList filter;
            if(filterId == Guid.Empty)
            {
                filter = new FilterList()
                {
                    AuthorId = Context.User.Id
                };
                await DB.Filters.AddAsync(filter);
            } else
            {
                filter = await DB.GetFilter(filterId);
            }
            if (filter == null || filter.AuthorId != Context.User.Id)
            {
                await RespondRaw("Not found", 404);
                return;
            }
            if (data.name.IsSpecified)
                filter.Name = data.name.Value;
            if(data.text.IsSpecified) 
                filter.Text = data.text.Value;
            if(data.template.IsSpecified)
                filter.AutoAddTemplate = data.template.Value;
            await DB.SaveChangesAsync();
            await RespondRaw(filter.Id.ToString());
        }

        [Method("DELETE"), Path("/api/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public async Task APIDeleteFilter(Guid filterId)
        {
            var filter = await DB.GetFilter(filterId);
            if(filter == null || filter.AuthorId != Context.User.Id)
            {
                await RespondRaw("Not found", 404);
                return;
            }
            DB.DeleteFilter(filter);
            await DB.SaveChangesAsync();
            await RedirectTo(nameof(ListFilters));
        }


        [Method("GET"), Path("/filters-raw/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        [RequireNoExcessQuery(false)]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        public async Task FetchRaw(Guid filterId)
        {
            var filter = await DB.GetFilter(filterId);
            if(filter == null)
            {
                await RespondRaw("No filter exists by that ID", 404);
                return;
            }
            await RespondRaw(filter.Text);
        }

    }
}
