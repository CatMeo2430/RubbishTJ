using System;
using System.Collections.Generic;
using System.IO;
using Taiji.Core.Models;

namespace Taiji.Core.Utils
{
    /// <summary>本地图片 → completions.files[]（无独立上传接口）。</summary>
    public static class ImageEncoder
    {
        private static readonly HashSet<string> AllowedExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp"
        };

        public static ChatFilePayload EncodeFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new ApiException($"文件不存在: {path}");

            var ext = Path.GetExtension(path);
            if (!AllowedExt.Contains(ext))
                throw new ApiException($"不支持的图片类型: {(string.IsNullOrEmpty(ext) ? "(无扩展名)" : ext)}");

            var mime = GuessMime(ext);
            var bytes = File.ReadAllBytes(path);
            var b64 = Convert.ToBase64String(bytes);
            return new ChatFilePayload
            {
                Name = Path.GetFileName(path),
                Data = $"data:{mime};base64,{b64}"
            };
        }

        public static List<ChatFilePayload> EncodeFiles(
            IEnumerable<string> paths,
            int maxCount = Constant.DefaultMaxFileCount,
            int maxMb = Constant.DefaultMaxFileMb)
        {
            var list = new List<ChatFilePayload>();
            var limit = (long)maxMb * 1024 * 1024;
            foreach (var path in paths)
            {
                if (list.Count >= maxCount)
                    throw new ApiException($"最多 {maxCount} 张图片");
                var fi = new FileInfo(path);
                if (!fi.Exists)
                    throw new ApiException($"文件不存在: {path}");
                if (fi.Length > limit)
                    throw new ApiException($"{fi.Name} 超过 {maxMb}MB 限制");
                list.Add(EncodeFile(path));
            }
            return list;
        }

        private static string GuessMime(string ext)
        {
            switch (ext.ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".bmp": return "image/bmp";
                default: return "application/octet-stream";
            }
        }
    }
}
