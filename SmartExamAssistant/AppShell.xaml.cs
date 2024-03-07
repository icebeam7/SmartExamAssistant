using SmartExamAssistant.Views;

namespace SmartExamAssistant
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(
                nameof(ExamDetailView), 
                typeof(ExamDetailView));
        }
    }
}
