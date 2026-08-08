namespace HomeMind.Business.IServices.AI;

/// <summary>
/// 剪辑 MCP 客户端契约：按剪辑方案生成剪映 .draft 草稿内容（字节流），供生成文件登记使用。
/// 独立于 CreatorMcp，遵守本地优先原则；B25 提供确定性 Mock 实现（MockClippingMcpClient），
/// 真实 jianying-mcp / capcut-mate 接入（依赖可访问素材与剪映草稿目录的主机部署）为部署环境验证项。
/// 实现不得返回草稿绝对路径、素材目录内容或 Prompt。
/// </summary>
public interface IClippingMcpClient
{
    /// <summary>按剪辑方案（运行动作 RequestJson，含素材位置、创作指令、片段序列与总时长）生成 .draft 草稿内容。</summary>
    /// <param name="planJson">剪辑方案 JSON（蛇形键），由 SkillRun 运行动作的 RequestJson 承载。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>草稿文件内容字节流（非空），由调用方登记为生成文件。</returns>
    Task<byte[]> GenerateDraftAsync(string planJson, CancellationToken cancellationToken = default);
}
