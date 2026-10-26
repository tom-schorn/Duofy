using Duofy.Schemas;

namespace Duofy.ApiClient;

/// <summary>
/// Kategorien über die Duofy-API (/api/v1/categories): die eingebauten lesen, eigene anlegen,
/// umbenennen und löschen.
/// </summary>
public interface ICategoryApi
{
    /// <summary>
    /// Liefert die eingebauten und die eigenen Kategorien, nach Gruppen geordnet; mit <paramref name="owner"/> die
    /// eines Mitglieds, das irgendetwas freigegeben hat (Spec 68), damit man für es buchen und planen kann.
    /// </summary>
    Task<ApiResult<IReadOnlyList<CategoryResponse>>> ListAsync(Guid? owner = null, CancellationToken cancellationToken = default);

    /// <summary>Legt eine eigene Kategorie an.</summary>
    Task<ApiResult<CategoryResponse>> CreateAsync(SaveCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ändert Name, Gruppe und Topf einer eigenen Kategorie.</summary>
    Task<ApiResult<CategoryResponse>> UpdateAsync(Guid id, SaveCategoryRequest request, CancellationToken cancellationToken = default);

    /// <summary>Löscht eine eigene Kategorie, solange keine Buchung sie benutzt (sonst <c>category_in_use</c>).</summary>
    Task<ApiResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
