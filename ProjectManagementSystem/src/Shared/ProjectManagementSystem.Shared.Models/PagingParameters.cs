namespace ProjectManagementSystem.Shared.Models;

/// <summary>
/// Paging parameters for paginated requests
/// </summary>
public class PagingParameters
{
    private int _pageSize = 10;
    private int _pageNumber = 1;

    /// <summary>
    /// Page number (1-based)
    /// </summary>
    public int PageNumber 
    { 
        get => _pageNumber; 
        set => _pageNumber = value > 0 ? value : 1; 
    }

    /// <summary>
    /// Number of items per page
    /// </summary>
    public int PageSize 
    { 
        get => _pageSize; 
        set => _pageSize = value > 0 ? (value > 100 ? 100 : value) : 10; 
    }

    /// <summary>
    /// Number of items to skip
    /// </summary>
    public int Skip => (PageNumber - 1) * PageSize;

    /// <summary>
    /// Number of items to take
    /// </summary>
    public int Take => PageSize;
}