namespace Forge.App.ViewModels;

/// <summary>Grupo ou aba que se renomeia no lugar (duplo clique ou F2), sem diálogo.</summary>
public interface IRenamable
{
    bool IsRenaming { get; set; }
}
