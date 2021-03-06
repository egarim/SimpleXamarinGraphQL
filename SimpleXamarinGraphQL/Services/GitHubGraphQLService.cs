﻿using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Client.Abstractions;
using GraphQL.Client.Http;
using ModernHttpClient;
using Polly;

namespace SimpleXamarinGraphQL
{
    public static class GitHubGraphQLService
    {
        static readonly Lazy<GraphQLHttpClient> _client = new Lazy<GraphQLHttpClient>(CreateGitHubGraphQLClient);

        static GraphQLHttpClient Client => _client.Value;

        public static async Task<GitHubUser> GetGitHubUser(string login)
        {
            var graphQLRequest = new GraphQLRequest
            {
                Query = "query { user(login: \"" + login + "\"){ name, company, createdAt, followers{ totalCount }}}"
            };

            var gitHubUserResponse = await AttemptAndRetry(() => Client.SendQueryAsync<GitHubUserGraphQLResponse>(graphQLRequest)).ConfigureAwait(false);

            return gitHubUserResponse.User;
        }

        static GraphQLHttpClient CreateGitHubGraphQLClient()
        {
            var graphQLOptions = new GraphQLHttpClientOptions
            {
                EndPoint = new Uri(GitHubConstants.GraphQLApiUrl),
                HttpMessageHandler = new NativeMessageHandler(),
            };

            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue(nameof(SimpleXamarinGraphQL))));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("bearer", GitHubConstants.PersonalAccessToken);

            return new GraphQLHttpClient(graphQLOptions, client);
        }

        static async Task<T> AttemptAndRetry<T>(Func<Task<GraphQLResponse<T>>> action, int numRetries = 2)
        {
            var response = await Policy.Handle<Exception>().WaitAndRetryAsync(numRetries, pollyRetryAttempt).ExecuteAsync(action).ConfigureAwait(false);

            if (response.Errors != null && response.Errors.Count() > 1)
                throw new AggregateException(response.Errors.Select(x => new Exception(x.ToString())));

            if (response.Errors != null && response.Errors.Count() is 1)
                throw new Exception(response.Errors.First().ToString());

            return response.Data;

            static TimeSpan pollyRetryAttempt(int attemptNumber) => TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
        }
    }
}
