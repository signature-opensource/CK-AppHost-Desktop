using CK.AppHost.Desktop;
using CK.AppIdentity;
using CK.Core;
using CK.Setup;
using CK.Testing;
using CK.TypeScript;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.AppHost.DesktopNgApp.Tests;

[TestFixture]
public class DesktopNgAppTests
{
    #region The aspect

    static UserMessageCollector Collector() => new UserMessageCollector( new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.CodeDefault ) );

    static DesktopNgAppConfiguration? Read( string json, UserMessageCollector collector )
    {
        ConfigurationModelReader.TryRead<DesktopNgAppConfiguration>( ImmutableConfigurationSection.CreateFromJson( "Aspects", "DesktopNgApp", json ),
                                                                     "Aspects:DesktopNgApp",
                                                                     collector,
                                                                     out var configuration );
        return configuration;
    }

    static AppBuilderContext CreateContext( string app )
    {
        var engine = new EngineConfiguration();
        var section = ImmutableConfigurationSection.CreateFromJson( "ckonf:Apps", "Sample", app );
        return new AppBuilderContext( engine, [new AppConfiguration( "Sample", TestHelper.TestProjectFolder, TestHelper.TestProjectFolder, section, engine.FirstBinPath )] );
    }

    [Test]
    public void the_aspect_projects_the_settings_of_the_window()
    {
        var context = CreateContext( """{ "Local": { "PartyClient": { "App": { "NgApp": true } } } }""" );
        var aspect = new DesktopNgAppAspect( Read( """{ "Client": "App", "Width": 800 }""", Collector() ).ShouldNotBeNull() );
        aspect.Apply( TestHelper.Monitor, context, context.Apps[0] ).ShouldBeTrue();
        context.Apps[0].ProjectedValues.OrderBy( kv => kv.Key ).Select( kv => $"{kv.Key}={kv.Value}" )
               .ShouldBe( ["DesktopNgApp:Client=App", "DesktopNgApp:Height=800", "DesktopNgApp:Title=App", "DesktopNgApp:Width=800"],
                          "The title defaults to the client name, the height to 800." );
    }

    [Test]
    public void the_client_must_be_a_client_of_the_App()
    {
        var context = CreateContext( """{ "Local": { "PartyClient": { "Other": { "NgApp": true } } } }""" );
        var aspect = new DesktopNgAppAspect( Read( """{ "Client": "App" }""", Collector() ).ShouldNotBeNull() );
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            aspect.Apply( TestHelper.Monitor, context, context.Apps[0] ).ShouldBeFalse();
            texts.ShouldContain( t => t.Contains( "the App has no \"Local:PartyClient:App\"" ) );
        }
    }

    [TestCase( """{ "Title": "No client" }""" )]
    [TestCase( """{ "Client": "App", "Width": 0 }""" )]
    [TestCase( """{ "Client": "App", "Color": "Blue" }""" )]
    public void an_invalid_configuration_is_an_error( string json )
    {
        var collector = Collector();
        Read( json, collector ).ShouldBeNull();
        collector.UserMessages.ShouldContain( m => m.Level == UserMessageLevel.Error );
    }

    [Test]
    public void the_runner_reads_the_projected_settings()
    {
        static IConfiguration C( params (string, string)[] values )
            => new ConfigurationBuilder().AddInMemoryCollection( values.Select( v => new KeyValuePair<string, string?>( v.Item1, v.Item2 ) ) ).Build();

        DesktopNgAppRunner.ReadSettings( C( ("DesktopNgApp:Client", "App"), ("DesktopNgApp:Title", "The title"), ("DesktopNgApp:Width", "800"), ("DesktopNgApp:Height", "600") ) )
                          .ShouldBe( new DesktopWindowSettings( "The title", 800, 600 ) );
        DesktopNgAppRunner.ReadSettings( C( ("DesktopNgApp:Client", "App") ) )
                          .ShouldBe( new DesktopWindowSettings( "App", 1280, 800 ) );
    }

    [TestCase( "CK/$Configurator/#Production", "CK-Configurator" )]
    [TestCase( "Sample/Auth3/$Front/#Dev", "Sample-Auth3-Front" )]
    public void the_folder_of_the_logs_of_a_published_application( string fullName, string expected )
    {
        DesktopHostRunner.GetLogFolderName( fullName ).ShouldBe( expected );
    }

    #endregion

    #region The url of the client

    /// <summary>
    /// A web build ("TestBuild") that stands for "NgApp" (CK.TS.Angular): the client application of the ClientDirectory.
    /// </summary>
    [PartyClientFeature( "TestBuild", TypeActivation.Optional )]
    public sealed class TestBuildClientFeature : ClientFeature, IWebAppBuild
    {
        public TestBuildClientFeature( PartyClient client, ImmutableConfigurationSection configuration, ClientInfo info )
            : base( client, configuration )
        {
            Info = info;
        }

        public ClientInfo Info { get; }

        public NormalizedPath ProjectPath => Info.ProjectPath;

        public NormalizedPath OutputPath => Info.OutputPath;

        public System.Diagnostics.ProcessStartInfo GetDevServerStart( string yarnPath, int port ) => new( "node", $"\"{yarnPath}\" ng serve --port {port}" );
    }

    public sealed class TestBuildClientFeatureDriver : ClientFeatureDriver<TestBuildClientFeature>
    {
        readonly ClientDirectory _directory;

        public TestBuildClientFeatureDriver( ClientDirectory directory ) => _directory = directory;

        protected override TestBuildClientFeature? Create( IActivityMonitor monitor, PartyClient client, ImmutableConfigurationSection configuration )
            => new( client, configuration, _directory.Clients.Single( c => c.Name == client.Name ) );
    }

    static IStObjMap? _map;

    // The App (served at "/") and the MiniApp (served at "/mini-app"), both built: the host runs in Production.
    static async Task<IStObjMap> GetMapAsync()
    {
        if( _map == null )
        {
            var root = TestHelper.CleanupFolder( TestHelper.GetTypeScriptGeneratedOnlyTargetProjectPath(), false );
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.Path = TestHelper.BinFolder;
            configuration.FirstBinPath.Assemblies.Add( AspNetServerTestHelperExtensions.HttpEndpointAssembly );
            configuration.FirstBinPath.Types.Add( typeof( ClientDirectory ),
                                                  typeof( ClientsHosting ),
                                                  typeof( PartyComponentDirectory ),
                                                  typeof( PartyClientFeatureDriver ),
                                                  typeof( HostingClientFeature ),
                                                  typeof( HostingClientFeatureDriver ),
                                                  typeof( TestBuildClientFeature ),
                                                  typeof( TestBuildClientFeatureDriver ),
                                                  typeof( DesktopNgAppRunner ) );
            configuration.EnsureAspect<TypeScriptAspectConfiguration>();
            var app = configuration.FirstBinPath.EnsureAspect<TypeScriptBinPathAspectConfiguration>();
            app.Name = "App";
            app.Kind = ClientKind.DefaultApp;
            app.DevServerPort = 4200;
            app.TargetProjectPath = root.AppendPart( "App" );
            app.IntegrationMode = CKGenIntegrationMode.None;
            app.AddOtherConfiguration( new TypeScriptBinPathAspectConfiguration
            {
                Name = "MiniApp",
                Kind = ClientKind.App,
                DevServerPort = 4201,
                TargetProjectPath = root.AppendPart( "MiniApp" ),
                IntegrationMode = CKGenIntegrationMode.None
            } );
            _map = (await configuration.RunSuccessfullyAsync()).LoadMap();
            CreateBuiltClient( root.AppendPart( "App" ), "App" );
            CreateBuiltClient( root.AppendPart( "MiniApp" ), "MiniApp" );
        }
        return _map;

        static void CreateBuiltClient( NormalizedPath projectPath, string name )
        {
            var output = projectPath.Combine( $"dist/{name}/browser" );
            Directory.CreateDirectory( output );
            File.WriteAllText( output.AppendPart( "index.html" ), name );
        }
    }

    // Both clients are declared (web build and hosting); "DesktopNgApp:Client" is the projection of the aspect.
    static async Task<RunningAspNetServer> CreateHostAsync( string? client )
    {
        return await (await GetMapAsync()).CreateRunningAspNetServerAsync( identity: c =>
        {
            foreach( var name in new[] { "App", "MiniApp" } )
            {
                c[$"Local:PartyClient:{name}:TestBuild"] = "true";
                c[$"Local:PartyClient:{name}:Hosting"] = "true";
            }
        }, configureHost: b =>
        {
            b.Environment.EnvironmentName = "Production";
            if( client != null ) b.Configuration.AddInMemoryCollection( [new KeyValuePair<string, string?>( "DesktopNgApp:Client", client )] );
        } );
    }

    [Test]
    public async Task the_runner_is_the_real_object_of_the_definer_Async()
    {
        var map = await GetMapAsync();
        // CKomposableAppHost finds the runner by its interface.
        map.StObjs.Obtain<IAppHostRunner>().ShouldBeOfType<DesktopNgAppRunner>();
        // The definer is not a real object of the map (its specialization is): the handlers depend on the specialization.
        map.StObjs.Obtain<DesktopHostRunner>().ShouldBeNull();
        map.StObjs.Obtain<DesktopNgAppRunner>().ShouldNotBeNull();
    }

    [Test]
    public async Task the_window_shows_its_client_where_the_endpoint_serves_it_Async()
    {
        await using var server = await CreateHostAsync( "MiniApp" );
        DesktopNgAppRunner.GetClientUrl( server.Services, out var url ).ShouldBeNull();
        url.ShouldNotBeNull().ShouldEndWith( "/mini-app/" );
        (await (await server.Client.GetAsync( new Uri( url ).AbsolutePath )).Content.ReadAsStringAsync()).ShouldBe( "MiniApp" );
    }

    [TestCase( null, "No \"DesktopNgApp:Client\" in the configuration" )]
    [TestCase( "Nope", "The client 'Nope' that the desktop window shows is not a client of the party." )]
    public async Task without_its_client_the_window_shows_a_failure_Async( string? client, string message )
    {
        await using var server = await CreateHostAsync( client );
        DesktopNgAppRunner.GetClientUrl( server.Services, out var url ).ShouldNotBeNull().ShouldStartWith( message );
        url.ShouldBeNull();
    }

    #endregion
}
