namespace StudentManagementSystem.BusinessLayer.DTOs.Common
{
    public class PaginatedResponse<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;

        public PaginatedResponse() { }

        public PaginatedResponse(IEnumerable<T> items, int totalItems, int currentPage, int pageSize)
        {
            Items = items;
            TotalItems = totalItems;
            CurrentPage = currentPage;
            PageSize = pageSize;
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        }
    }
}
