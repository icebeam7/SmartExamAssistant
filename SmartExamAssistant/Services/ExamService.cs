﻿using System.Text.Json;
using System.Text.RegularExpressions;

using Azure;
using Azure.AI.OpenAI;

using SmartExamAssistant.Models;
using SmartExamAssistant.Helpers;

namespace SmartExamAssistant.Services
{
    public class ExamService : IExamService
    {
        OpenAIClient oaiClient;
        IDatabaseService dbService;
        IPDFService pdfService;

        public ExamService(IDatabaseService dbService, IPDFService pdfService)
        {
            this.dbService = dbService;
            this.pdfService = pdfService;

            oaiClient = new OpenAIClient(
                new Uri(Constants.AzureOpenAIEndpoint),
                new AzureKeyCredential(Constants.AzureOpenAIKey));
        }

        public async Task<Exam> GenerateNewExamAsync(string message)
        {
            var userPrompt = "Limita tus respuestas a la información obtenida desde los documentos. " +
            "Responde la información en español en un arreglo llamado questions" +
            "en formato json con los campos Number, Content, " +
            "OptionA, OptionB, OptionC, OptionD (agrega la letra " +
            "A, B, C o D según corresponda al texto de la opción)," +
            "CorrectOption (la cual puede ser A, B, C o D), " +
            "Reference. " +
            "No incluyas la referencia en el contenido de la pregunta, solo en " +
            "el campo Reference. " +
            "\n\nSOLO EL FORMATO JSON ESTÁ PERMITIDO en la respuesta. " +
            "No se permite explicación u otro texto." +
            "\n\nMensaje del usuario: ###\n" +
            "Quiero generar un examen de 3 preguntas de opción múltiple " +
            "sobre el tema: \"" + message + "\"" +
            "\n\n###";

            try
            {
                var chatCompletionsOptions = new ChatCompletionsOptions()
                {
                    Messages =               
                    {
                        new ChatRequestSystemMessage(Constants.SystemMessage),
                        new ChatRequestUserMessage(userPrompt),
                    },
                    AzureExtensionsOptions = new AzureChatExtensionsOptions()
                    {
                        Extensions =
                        {
                            new AzureCognitiveSearchChatExtensionConfiguration()
                            {
                                ShouldRestrictResultScope = true,
                                DocumentCount = 5,
                                SearchEndpoint = new Uri(Constants.AzureSearchEndpoint),
                                IndexName = Constants.AzureSearchIndex,
                                Key = Constants.AzureSearchKey,
                                QueryType = AzureCognitiveSearchQueryType.Simple,
                                //FieldMappingOptions = new()
                                //{
                                //    TitleFieldName = "DocumentName",
                                //    FilepathFieldName = "RowId"
                                //}
                            },
                        },
                    },

                    DeploymentName = Constants.AzureOpenAIDeployment,
                };

                var response = await oaiClient.GetChatCompletionsAsync(chatCompletionsOptions);

                var assistantMessage = response.Value.Choices.FirstOrDefault()?.Message;
                var exam = new Exam() { Topic = message };

                if (assistantMessage != null)
                {
                    var content = assistantMessage.Content;

                    try
                    {
                        var questions =
                            JsonSerializer.Deserialize<Questions>(content);

                        exam.Questions = questions.questions;

                        if (exam.Questions != null)
                        {
                            var contextContent = assistantMessage
                                .AzureExtensionsContext
                                .Messages
                                .FirstOrDefault()?
                                .Content;

                            var references =
                                JsonSerializer.Deserialize<
                                    References>(contextContent);

                            exam.Citations = references.citations;
                        }
                        else
                        {

                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }

                return exam;
            }
            catch (Exception ex)
            {
                return default;
            }
        }

        public async Task<List<Exam>> GetExamsAsync()
        {
            var dbExams = await dbService.GetItemsAsync<DbModels.Exam>();
            var exams = new List<Exam>();

            foreach (var exam in dbExams)
            {
                exams.Add(new()
                {
                    Id = exam.Id,
                    Topic = exam.Topic
                });
            }

            return exams;
        }

        public async Task<Exam> GetExamWithDetailsAsync(int id)
        {
            var dbExam = await dbService.GetItemAsync<DbModels.Exam>(id);

            var dbQuestions = (await dbService
                            .GetItemsAsync<DbModels.Question>())
                            .Where(x => x.ExamId == dbExam.Id);

            var dbCitations = await dbService.GetItemsAsync<DbModels.Citation>();

            var questions = new List<Question>();
            var citations = new List<Citation>();

            foreach (var dbQuestion in dbQuestions)
            {
                var dbCitation = dbQuestion.Citation;

                questions.Add(new()
                {
                    Number = dbQuestion.Number,
                    Content = dbQuestion.Content,
                    OptionA = dbQuestion.OptionA,
                    OptionB = dbQuestion.OptionB,
                    OptionC = dbQuestion.OptionC,
                    OptionD = dbQuestion.OptionD,
                    CorrectOption = dbQuestion.CorrectOption,
                    Reference = dbQuestion.Reference,
                    Citation = new()
                    {
                        filepath = dbCitation.FilePath,
                        title = dbCitation.Title,
                        url = dbCitation.Url,
                    },
                    SelectedOption = string.Empty
                });

                if (!citations.Any(x =>
                    x.filepath == dbCitation.FilePath &&
                    x.title == dbCitation.Title &&
                    x.url == dbCitation.Url))
                {
                    citations.Add(new()
                    {
                        filepath = dbCitation.FilePath,
                        title = dbCitation.Title,
                        url = dbCitation.Url,
                    });
                }
            }

            return new()
            {
                Id = dbExam.Id,
                Topic = dbExam.Topic,
                Questions = questions,
                Citations = citations
            };
        }

        public async Task SaveExamAsync(Exam exam)
        {
            DbModels.Exam dbExam = new()
            {
                Topic = exam.Topic,
            };

            await dbService.AddItemAsync(dbExam);

            var citations = exam.Citations.ToList();
            List<DbModels.Citation> dbCitations = new();

            foreach (var citation in exam.Citations)
            {
                dbCitations.Add(new()
                {
                    FilePath = citation.filepath,
                    Title = citation.title,
                    Url = citation.url
                });
            }

            await dbService.AddItemsAsync(dbCitations);

            List<DbModels.Question> dbQuestions = new();

            foreach (var question in exam.Questions)
            {
                var references = FindCitation(question.Reference, citations);
                var refIndex = citations.IndexOf(references.First());

                dbQuestions.Add(new()
                {
                    Number = question.Number,
                    Content = question.Content,
                    OptionA = question.OptionA,
                    OptionB = question.OptionB,
                    OptionC = question.OptionC,
                    OptionD = question.OptionD,
                    CorrectOption = question.CorrectOption,
                    Reference = question.Reference,
                    ExamId = dbExam.Id,
                    CitationId = dbCitations[refIndex].Id
                });
            }

            await dbService.AddItemsAsync(dbQuestions);
        }

        public async Task DeleteExamAsync(Exam exam)
        {
            var dbExam = await dbService.GetItemAsync<DbModels.Exam>(exam.Id);
            await dbService.DeleteItemAsync(dbExam);
        }

        string referencePattern = @"(?<=\[doc).*?(?=\])";

        public IEnumerable<Citation> FindCitation(string reference, 
            List<Citation> citations)
        {
            if (!string.IsNullOrEmpty(reference))
            {
                var matches = Regex.Matches(reference, referencePattern);

                foreach (Match match in matches)
                {
                    int id;
                    int.TryParse(match.Captures[0].Value, out id);
                    
                    yield return id > 0
                        ? citations[id - 1] 
                        : new();
                }
            }
            else
                yield return new ();
        }

        public async Task ViewPDFAsync(Exam exam)
        {
            await pdfService.GeneratePDFAsync(exam);
        }

    }
}
