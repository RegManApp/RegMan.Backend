using StudentManagementSystem.BusinessLayer.Contracts;
using StudentManagementSystem.DAL.Contracts;
using StudentManagementSystem.DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudentManagementSystem.BusinessLayer.Services
{
    internal class SectionService:ISectionService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IBaseRepository<Section> sectionRepository;
        public SectionService(IUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
            this.sectionRepository = unitOfWork.Sections;
        }

    }
}
