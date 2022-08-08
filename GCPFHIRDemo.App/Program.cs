using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.PubSub.V1;
using Grpc.Auth;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;

namespace GCPFHIRDemo.App
{
    internal class Program
    {
        private const string PROJECT_ID = "";
        private const string LOCATION = "us-east4";
        private const string DATA_SET = "dsGCPDDEMO001";
        private const string FHIR_STORE = "fdsGCPDDEMO001";

        private static string GetFHIRRepositoryPath()
        {
            return string.Format("/projects/{0}/locations/{1}/datasets/{2}/fhirStores/{3}/fhir/", PROJECT_ID, LOCATION, DATA_SET, FHIR_STORE);
        }

        private static string FHIRRepositoryPath = "";

        static void Main(string[] args)
        {
            //Run().ConfigureAwait(false).GetAwaiter().GetResult();
            Sub().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public static async System.Threading.Tasks.Task Run()
        {

            var patient = new Patient()
            {
                Name = new List<HumanName>()
                {
                    new HumanName()
                    {
                        Use = HumanName.NameUse.Official,
                        Family = "Smith",
                        Given = new List<string>()
                        {
                            "Jenny"
                        }
                    }
                },
                Gender = AdministrativeGender.Female,
                BirthDate = "2022-01-01",
                Id = "1000004"
            };

            var fhirClient = GetFhirClient();
             
            var fhirRepositoryPatient = await fhirClient.UpdateAsync(patient);
        }

        public static async System.Threading.Tasks.Task Sub()
        {
            var projectId = "";
            var subscriptionId = "patient-fdsGCPDDEMO001";

            var googleCredentials = GoogleCredential.FromFile("C:\\users\\zgardner\\Downloads\\f4fd88b0e369.json");

            var subscriptionName = new SubscriptionName(projectId, subscriptionId);
            var subscriberClientBuilder = new SubscriberClientBuilder()
            {
                ChannelCredentials = googleCredentials.ToChannelCredentials(),
                SubscriptionName = subscriptionName
            };

            var subscriberClient = await subscriberClientBuilder.BuildAsync();

            var fhirClient = GetFhirClient();

            await subscriberClient.StartAsync(async (message, cancellationToken) =>
            {
                var messageData = message.Data.ToStringUtf8();
                var fhirRepositoryPatient = await fhirClient.ReadAsync<Patient>(messageData);
                return SubscriberClient.Reply.Ack;
            });

            while(true)
            {
                Thread.Sleep(1000);
            }
        }

        private static FhirClient GetFhirClient()
        {
            var bearerToken = "";

            var fhirRepositoryUrl = string.Format("https://healthcare.googleapis.com/v1{0}", GetFHIRRepositoryPath());

            var fhirClientSettings = new FhirClientSettings()
            {
                Timeout = 120000,
                PreferredFormat = ResourceFormat.Json,
                VerifyFhirVersion = false,
                PreferredReturn = Prefer.ReturnMinimal,
            };

            var gcpFHIRRepositoryMessageHandler = new GCPFHIRRepositoryMessageHandler(bearerToken);

            return new FhirClient(fhirRepositoryUrl, fhirClientSettings, gcpFHIRRepositoryMessageHandler);
        }
    }

    internal class GCPFHIRRepositoryMessageHandler : HttpClientHandler
    {
        private readonly AuthenticationHeaderValue authenticationHeaderValue = null;

        public GCPFHIRRepositoryMessageHandler(string bearerToken)
        {
            if (!string.IsNullOrEmpty(bearerToken))
            {
                authenticationHeaderValue = new AuthenticationHeaderValue("Bearer", bearerToken);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (authenticationHeaderValue != null)
            {
                request.Headers.Authorization = authenticationHeaderValue;
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
