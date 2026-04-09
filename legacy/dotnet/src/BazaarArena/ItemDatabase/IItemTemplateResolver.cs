using BazaarArena.Core;

namespace BazaarArena.ItemDatabase;

/// <summary>按物品名称解析模板，供卡组校验与战斗实例化使用。</summary>
public interface IItemTemplateResolver
{
    /// <summary>根据中文名称获取物品模板，不存在则返回 null。</summary>
    ItemTemplate? GetTemplate(string name);
}
