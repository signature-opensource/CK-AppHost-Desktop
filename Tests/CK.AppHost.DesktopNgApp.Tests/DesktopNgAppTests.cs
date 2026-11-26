using CK.AppHost.Desktop;
using CK.AppIdentity;
using CK.Core;
using CK.Setup;
using CK.Testing;
using CK.TS.Angular;
using CK.TypeScript;
using Microsoft.Extensions.DependencyInjection;
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
    #region The option

    static UserMessageCollector Collector() => new UserMessageCollector( new CurrentCultureInfo( new TranslationService(), NormalizedCultureInfo.CodeDefault ) );

    static DesktopNgAppConfiguration? Read( string json, UserMessageCollector collector )
    {
        ConfigurationModelReader.TryRead<DesktopNgAppConfiguration>( ImmutableConfigurationSection.CreateFromJson( "NgApp", "DesktopNgApp", json ),
                                                                     "NgApp:DesktopNgApp",
                                                                     collector,
                                                                     out var configuration );
        return configuration;
    }

    [TestCase( """{ "Width": 0 }""" )]
    [TestCase( """{ "Color": "Blue" }""" )]
    [TestCase( """{ "Client": "App" }""" )]
    public void an_invalid_configuration_is_an_error( string json )
    {
        var collector = Collector();
        Read( json, collector ).ShouldBeNull();
        collector.UserMessages.ShouldContain( m => m.Level == UserMessageLevel.Error );
    }

    // The App builder: the AppIdentity contributor, then the DesktopNgApp one.
    static (AppBuilderContext Context, bool Success) Contribute( string app )
    {
        var engine = new EngineConfiguration();
        var section = ImmutableConfigurationSection.CreateFromJson( "ckonf:Apps", "Sample", app );
        var a = new AppConfiguration( "Sample", TestHelper.TestProjectFolder, TestHelper.TestProjectFolder, section, engine.FirstBinPath );
        a.ConfigurableTypes = [typeof( NgAppClientFeature ), typeof( HostingClientFeature ), typeof( DesktopNgAppOption )];
        var context = new AppBuilderContext( engine, [a] );
        var appIdentity = new AppIdentityAppBuilderContributor();
        var success = appIdentity.Contribute( TestHelper.Monitor, context );
        success &= new DesktopNgAppAppBuilderContributor( appIdentity ).Contribute( TestHelper.Monitor, context );
        return (context, success);
    }

    [Test]
    public void the_build_makes_the_endpoint_in_process()
    {
        var (context, success) = Contribute( """
            { "FullName": "Sample/$Sample",
              "Local": { "PartyClient": {
                "App": { "NgApp": { "DesktopNgApp": { "Title": "The title" } }, "Hosting": true },
                "Other": { "NgApp": true, "Hosting": true } } } }
            """ );
        success.ShouldBeTrue();
        context.Apps[0].ProjectedValues.ShouldBe( [KeyValuePair.Create( DesktopNgAppAppBuilderContributor.InProcessPath, "true" )] );

        (context, success) = Contribute( """{ "FullName": "Sample/$Sample", "Local": { "PartyClient": { "App": { "NgApp": true, "Hosting": true } } } }""" );
        success.ShouldBeTrue();
        context.Apps[0].ProjectedValues.ShouldBeEmpty( "No client enables the option: not a desktop application." );
    }

    [Test]
    public void the_window_shows_one_client()
    {
        using( TestHelper.Monitor.CollectTexts( out var texts ) )
        {
            var (_, success) = Contribute( """
                { "FullName": "Sample/$Sample",
                  "Local": { "PartyClient": {
                    "App": { "NgApp": { "DesktopNgApp": true }, "Hosting": true },
                    "Other": { "NgApp": { "DesktopNgApp": true }, "Hosting": true } } } }
                """ );
            success.ShouldBeFalse();
            texts.ShouldContain( t => t.Contains( "The clients 'App', 'Other' of App 'Sample' enable the \"DesktopNgApp\" option: the desktop window shows one client." ) );
        }
    }

    [TestCase( "CK/$Configurator/#Production", "CK-Configurator" )]
    [TestCase( "Sample/Auth3/$Front/#Dev", "Sample-Auth3-Front" )]
    public void the_folder_of_the_logs_of_a_published_application( string fullName, string expected )
    {
        DesktopHostRunner.GetLogFolderName( fullName ).ShouldBe( expected );
    }

    #endregion

    #region The window and the url of its client

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
                                                  typeof( NgAppClientFeature ),
                                                  typeof( NgAppClientFeatureDriver ),
                                                  typeof( DesktopNgAppOption ),
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

    // Both clients are declared (web build and hosting); the option is enabled on the given clients.
    static void Declare( MutableConfigurationSection c, params string[] desktop )
    {
        foreach( var name in new[] { "App", "MiniApp" } )
        {
            if( desktop.Contains( name ) ) c[$"Local:PartyClient:{name}:NgApp:DesktopNgApp:Title"] = $"The {name}";
            else c[$"Local:PartyClient:{name}:NgApp"] = "true";
            c[$"Local:PartyClient:{name}:Hosting"] = "true";
        }
    }

    static async Task<RunningAspNetServer> CreateHostAsync( params string[] desktop )
    {
        return await (await GetMapAsync()).CreateRunningAspNetServerAsync( identity: c => Declare( c, desktop ),
                                                                           configureHost: b => b.Environment.EnvironmentName = "Production" );
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
    public async Task the_window_settings_are_read_before_the_host_starts_Async()
    {
        var map = await GetMapAsync();
        await using var services = map.CreateAutomaticServices( s =>
        {
            s.AddSingleton( ApplicationIdentityServiceConfiguration.Create( TestHelper.Monitor, c =>
            {
                c["FullName"] = "Sample/$Desktop";
                Declare( c, "MiniApp" );
                c["Local:PartyClient:MiniApp:NgApp:DesktopNgApp:Width"] = "640";
            } ).ShouldNotBeNull() );
        } );
        DesktopNgAppRunner.ReadSettings( TestHelper.Monitor, services.Services ).ShouldBe( new DesktopWindowSettings( "The MiniApp", 640, 800 ) );
    }

    [Test]
    public async Task the_window_shows_its_client_where_the_endpoint_serves_it_Async()
    {
        await using var server = await CreateHostAsync( "MiniApp" );
        DesktopNgAppRunner.GetClientUrl( server.Services, out var url ).ShouldBeNull();
        url.ShouldNotBeNull().ShouldEndWith( "/mini-app/" );
        (await (await server.Client.GetAsync( new Uri( url ).AbsolutePath )).Content.ReadAsStringAsync()).ShouldBe( "MiniApp" );
    }

    [Test]
    public async Task without_its_client_the_window_shows_a_failure_Async()
    {
        await using( var server = await CreateHostAsync() )
        {
            DesktopNgAppRunner.GetClientUrl( server.Services, out var url ).ShouldNotBeNull().ShouldStartWith( "No client of the party enables the \"DesktopNgApp\" option" );
            url.ShouldBeNull();
        }
        await using( var server = await CreateHostAsync( "App", "MiniApp" ) )
        {
            DesktopNgAppRunner.GetClientUrl( server.Services, out var url ).ShouldNotBeNull().ShouldStartWith( "The clients 'App', 'MiniApp' enable the \"DesktopNgApp\" option" );
            url.ShouldBeNull();
        }
    }

    #endregion
}
