using SmartExamAssistant.Models;

namespace SmartExamAssistant.Services
{
    public interface IPDFService
    {
        Task GeneratePDFAsync(Exam exam);
    }
}
