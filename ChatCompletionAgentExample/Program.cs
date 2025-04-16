using Azure.Identity;
using ChatCompletionAgentExample.Classes;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Plugins;
using Microsoft.SemanticKernel.Functions;
using Microsoft.SemanticKernel.Connectors.OpenAI; 
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatCompletionAgentExample;

public static class Program
{
    public static async Task Main()
    {
        Settings settings = new();

        Console.WriteLine("Initialize plugins...");
        GitHubSettings githubSettings = settings.GetSettings<GitHubSettings>();
    
        GitHubPlugin githubPlugin = new(githubSettings);
        var userProfile = await githubPlugin.GetUserProfileAsync();

        Console.WriteLine("Creating kernel...");
        IKernelBuilder builder = Kernel.CreateBuilder();

        builder.AddAzureOpenAIChatCompletion(
        settings.AzureOpenAI.ChatModelDeployment,
        settings.AzureOpenAI.Endpoint,
        settings.AzureOpenAI.ApiKey);

        builder.Plugins.AddFromObject(githubPlugin);

        Kernel kernel = builder.Build();

        Console.WriteLine("Defining agent...");
        ChatCompletionAgent agent =
            new()
            {
                Name = "SampleAssistantAgent",
                Instructions =
                        """
                         You are Joe, a friendly and knowledgeable GitHub repository analyst with 8 years of experience in software development.
        
                        ## Your Persona:
                        - You are thoughtful, precise, and always explain your reasoning
                        - You have extensive knowledge about software architecture and best practices
                        - You use a warm, professional tone and occasionally add light humor
                        - You prefer to give structured responses with clear headings
                        
                        ## Your Capabilities:
                        - You can analyze GitHub repositories and provide insights
                        - You can access user profiles and repository details
                        - You can create plans to solve complex problems involving GitHub data
                        
                        The repository you are currently analyzing is: {{$repository}}
                        The current user you're assisting has username: {{$user.username}}
                        The current date and time is: {{$now}}
                        
                        Always break down your thought process and explain how you're approaching each question.
                        """,
                Kernel = kernel,
                Arguments = new KernelArguments()
                {
                    { "repository", "microsoft/semantic-kernel" },
                    { "user", new { username = userProfile.Login } }
                }

            };

        Console.WriteLine("Ready!");

        ChatHistoryAgentThread agentThread = new();
        bool isComplete = false;
        do
        {
            Console.WriteLine();
            Console.Write("> ");
            string input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }
            if (input.Trim().Equals("EXIT", StringComparison.OrdinalIgnoreCase))
            {
                isComplete = true;
                break;
            }

            var message = new ChatMessageContent(AuthorRole.User, input);

            Console.WriteLine();

            DateTime now = DateTime.Now;
            KernelArguments arguments =
                new()
                {
                    { "now", $"{now.ToShortDateString()} {now.ToShortTimeString()}" },
                    { "repository", "microsoft/semantic-kernel" },
                    { "user.username", userProfile.Login },
                };
            await foreach (ChatMessageContent response in agent.InvokeAsync(message, agentThread, options: new() { KernelArguments = arguments }))
            {
                // Display response.
                Console.WriteLine($"{response.Content}");
            }

        } while (!isComplete);
    }
}

