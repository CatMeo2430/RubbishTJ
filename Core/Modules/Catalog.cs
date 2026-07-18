using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Taiji.Core.Models;
using Taiji.Core.Utils;

namespace Taiji.Core.Modules
{
    internal sealed class Catalog
    {
        private readonly TaijiHttp _http;

        public Catalog(TaijiHttp http)
        {
            if (http == null) throw new ArgumentNullException("http");
            _http = http;
        }

        public ChatTmpl Tmpl { get; private set; }

        public async Task<ChatTmpl> LoadAsync(CancellationToken ct)
        {
            if (string.IsNullOrEmpty(_http.Token))
                throw new ApiException("未登录");

            var tmpl = await _http.GetDataAsync<ChatTmpl>("/chat/tmpl", ct).ConfigureAwait(false);
            if (tmpl == null)
                throw new ApiException("tmpl 为空");
            if (tmpl.Models == null)
                tmpl.Models = new List<ModelInfo>();
            if (tmpl.Providers == null)
                tmpl.Providers = new List<ProviderInfo>();
            if (tmpl.MFileCount <= 0)
                tmpl.MFileCount = Constant.DefaultMaxFileCount;
            if (tmpl.MFileSize <= 0)
                tmpl.MFileSize = Constant.DefaultMaxFileMb;

            Tmpl = tmpl;
            return tmpl;
        }

        public IList<ProviderInfo> GetProvidersOrdered()
        {
            EnsureLoaded();
            return Tmpl.Providers.OrderByDescending(p => p.Sort).ToList();
        }

        public IList<string> GetProviderNamesWithModels()
        {
            EnsureLoaded();
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var p in GetProvidersOrdered())
            {
                if (string.IsNullOrEmpty(p.Name) || seen.Contains(p.Name))
                    continue;
                if (ModelsByProviderName(p.Name).Count > 0)
                {
                    names.Add(p.Name);
                    seen.Add(p.Name);
                }
            }

            foreach (var m in Tmpl.Models)
            {
                var n = m.ProviderName;
                if (string.IsNullOrEmpty(n) || seen.Contains(n))
                    continue;
                names.Add(n);
                seen.Add(n);
            }

            return names;
        }

        public IList<ModelInfo> ModelsByProviderName(string providerName)
        {
            EnsureLoaded();
            var list = new List<ModelInfo>();
            foreach (var m in Tmpl.Models)
            {
                if (string.Equals(m.ProviderName, providerName, StringComparison.Ordinal))
                    list.Add(m);
            }
            return list;
        }

        public ModelInfo FindByValue(string value)
        {
            EnsureLoaded();
            return Tmpl.Models.FirstOrDefault(m => m.Value == value);
        }

        private void EnsureLoaded()
        {
            if (Tmpl == null)
                throw new ApiException("尚未加载模型列表，请先 LoadModelsAsync");
        }
    }
}
