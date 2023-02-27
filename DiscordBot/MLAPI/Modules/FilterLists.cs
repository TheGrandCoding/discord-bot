using DiscordBot.Classes.HTMLHelpers.Objects;
using DiscordBot.Services;
using Microsoft.Extensions.DependencyInjection;
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
        const string FilterIdRegex = "[a-zA-Z0-9]{8,}";
        public FilterLists(APIContext context) : base(context, "filters")
        {
            Service = Program.Services.GetRequiredService<FilterListService>();
        }
        public FilterListService Service { get; set; }

        [Method("GET"), Path("/filters")]
        public async Task ListFilters()
        {
            var folder = Service.GetDirectory(Context.User.Id);

            var ul = new UnorderedList();
            foreach(var file in Directory.EnumerateFiles(folder))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var anchor = new Anchor($"/filters/{id}");
                anchor.RawText = id;
                ul.AddItem(new ListItem() { Children = { anchor } });
            }
            var na = new Anchor($"#new", id: "new");
            na.RawText = "[Add new]";
            ul.AddItem(new ListItem() { Children = { na } });

            await ReplyFile("list.html", 200, new Replacements().Add("list", ul));
        }

        [Method("POST"), Path("/filters/new")]
        public async Task NewFilter()
        {
            if(!Service.TryCreateNew(Context.User.Id, out var id, out var fs))
            {
                await RespondRaw("Failed", 400);
                return;
            }
            try
            {
                fs.Write(Encoding.UTF8.GetBytes("New file."));
            } finally
            {
                fs.Close();
            }
            await RespondRedirect($"/filters/{id}");
        }

        [Method("GET"), Path("/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public async Task EditFilter(string filterId)
        {
            await ReplyFile("edit.html", 200);
        }

        [Method("DELETE"), Path("/filters/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public async Task DeleteFilter(string filterId)
        {
            if(Service.TryDelete(Context.User.Id, filterId))
            {
                await RespondRedirect("/filters");
            } else
            {
                await RespondRaw("Failed to delete", 500);
            }
        }

        [Method("GET"), Path("/filters-raw/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        [RequireNoExcessQuery(false)]
        [RequireAuthentication(false)]
        [RequireApproval(false)]
        public async Task FetchRaw(string filterId)
        {
            if(!Service.TryOpenRead(filterId, out var fs))
            {
                await RespondRaw("No filter exists by that ID", 404);
                return;
            }
            try
            {
                await ReplyStream(fs, 200);
            } finally
            {
                fs.Close();
            }
        }

        [Method("POST"), Path("/filters-raw/{filterId}")]
        [Regex("filterId", FilterIdRegex)]
        public async Task UploadRaw(string filterId, bool append = false)
        {
            if(!Service.TryOpenWrite(Context.User.Id, filterId, out var fs))
            {
                await RespondRaw("No filter exists by that ID or could not open file", 404);
                return;
            }
            try
            {
                var data = Encoding.UTF8.GetBytes(Context.Body);
                fs.Write(data);
            }
            finally
            {
                fs.Close();
            }
            await RespondRaw("OK");
        }
    }
}
