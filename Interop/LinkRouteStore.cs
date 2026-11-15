using System.IO;
using System.Text.Json;

namespace RobotVisionSortingTwin
{
    public sealed record LinkRoute(Guid ProductId, TwinProductKind Bin);

    /// <summary>仅保存三个料仓的稳定产品 GUID，不依赖平台文件路径或程序集。</summary>
    internal static class LinkRouteStore
    {
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RobotVisionSortingTwin", "product-routes.json");

        public static LinkRoute[] Load(string path)
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var routes = JsonSerializer.Deserialize<LinkRoute[]>(File.ReadAllText(path))
                ?? throw new InvalidDataException("料仓映射为空。");
            Validate(routes);
            return routes;
        }

        public static void Save(string path, LinkRoute[] routes)
        {
            Validate(routes);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(routes));
                File.Move(temporary, path, true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void Validate(LinkRoute[] routes)
        {
            if (routes.Length > 3 || routes.Any(route => route is null || route.ProductId == Guid.Empty
                || route.Bin is < TwinProductKind.Disc or > TwinProductKind.Plate)
                || routes.Select(route => route.Bin).Distinct().Count() != routes.Length
                || routes.Select(route => route.ProductId).Distinct().Count() != routes.Length)
            {
                throw new InvalidDataException("料仓映射存在重复或无效产品。");
            }
        }
    }
}
