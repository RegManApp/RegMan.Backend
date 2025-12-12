using StudentManagementSystem.BusinessLayer.DTOs.SectionDTOs;

namespace StudentManagementSystem.BusinessLayer.Contracts
{
    public interface ISectionService
    {
        Task<ViewSectionDTO> CreateSectionAsync(CreateSectionDTO sectionDTO);

    }
}
