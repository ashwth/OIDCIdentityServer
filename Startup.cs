using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OIDCIndetityServer.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OIDCIndetityServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllersWithViews();
            services.AddRazorPages();

            services.AddDbContext<ApplicationDbContext>(options =>
            {
                // Configure the context to use Microsoft SQL Server.
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnection"));

                // Register the entity sets needed by OpenIddict.
                // Note: use the generic overload if you need
                // to replace the default OpenIddict entities.
                options.UseOpenIddict();
            });

            // Register the Identity services.
            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders()
                .AddDefaultUI();



            // Configure Identity to use the same JWT claims as OpenIddict instead
            // of the legacy WS-Federation claims it uses by default (ClaimTypes),
            // which saves you from doing the mapping in your authorization controller.
            services.Configure<IdentityOptions>(options =>
            {
                options.ClaimsIdentity.UserNameClaimType = Claims.Name;
                options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
                options.ClaimsIdentity.RoleClaimType = Claims.Role;
                options.ClaimsIdentity.EmailClaimType = Claims.Email;

                // Note: to require account confirmation before login,
                // register an email sender service (IEmailSender) and
                // set options.SignIn.RequireConfirmedAccount to true.
                //
                // For more information, visit https://aka.ms/aspaccountconf.
                //options.SignIn.RequireConfirmedAccount = false;
            });


            // OpenIddict offers native integration with Quartz.NET to perform scheduled tasks
            // (like pruning orphaned authorizations/tokens from the database) at regular intervals.
            services.AddQuartz(options =>
            {
                options.UseMicrosoftDependencyInjectionJobFactory();
                options.UseSimpleTypeLoader();
                options.UseInMemoryStore();
            });


            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder =>
                    {
                        builder
                            //.AllowCredentials()
                            .AllowAnyOrigin()
                            //.WithOrigins(
                            //    "https://localhost:4200")
                            //.SetIsOriginAllowedToAllowWildcardSubdomains()
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    });
            });

            // Register the Quartz.NET service and configure it to block shutdown until jobs are complete.
            services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

            services.AddOpenIddict()

       // Register the OpenIddict core components.
       .AddCore(options =>
       {
            // Configure OpenIddict to use the Entity Framework Core stores and models.
            // Note: call ReplaceDefaultEntities() to replace the default OpenIddict entities.
            options.UseEntityFrameworkCore()
                  .UseDbContext<ApplicationDbContext>();

            // Developers who prefer using MongoDB can remove the previous lines
            // and configure OpenIddict to use the specified MongoDB database:
            // options.UseMongoDb()
            //        .UseDatabase(new MongoClient().GetDatabase("openiddict"));

            // Enable Quartz.NET integration.
            options.UseQuartz();
       }) // Register the OpenIddict server components.
        .AddServer(options =>
        {
            // Enable the authorization, device, logout, token, userinfo and verification endpoints.
            options.SetAuthorizationEndpointUris("/connect/authorize")
                   .SetDeviceEndpointUris("/connect/device")
                   .SetLogoutEndpointUris("/connect/logout")
                   .SetIntrospectionEndpointUris("/connect/introspect")
                   .SetTokenEndpointUris("/connect/token")
                   .SetUserinfoEndpointUris("/connect/userinfo")
                   .SetVerificationEndpointUris("/connect/verify");

            // Note: this sample uses the code, device code, password and refresh token flows, but you
            // can enable the other flows if you need to support implicit or client credentials.
            options.AllowAuthorizationCodeFlow()
                   .AllowDeviceCodeFlow()
                   .AllowHybridFlow()
                   .AllowRefreshTokenFlow();

            // Mark the "email", "profile", "roles" and "dataEventRecords" scopes as supported scopes.
            options.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.Roles, "dataEventRecords");

            // Register the signing and encryption credentials.
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();

            // Force client applications to use Proof Key for Code Exchange (PKCE).
            options.RequireProofKeyForCodeExchange();

            // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
            options.UseAspNetCore()
                   .EnableStatusCodePagesIntegration()
                   .EnableAuthorizationEndpointPassthrough()
                   .EnableLogoutEndpointPassthrough()
                   .EnableTokenEndpointPassthrough()
                   .EnableUserinfoEndpointPassthrough()
                   .EnableVerificationEndpointPassthrough()
                   .DisableTransportSecurityRequirement(); // During development, you can disable the HTTPS requirement.

            // Note: if you don't want to specify a client_id when sending
            // a token or revocation request, uncomment the following line:
            //
            // options.AcceptAnonymousClients();

            // Note: if you want to process authorization and token requests
            // that specify non-registered scopes, uncomment the following line:
            //
            // options.DisableScopeValidation();

            // Note: if you don't want to use permissions, you can disable
            // permission enforcement by uncommenting the following lines:
            //
            // options.IgnoreEndpointPermissions()
            //        .IgnoreGrantTypePermissions()
            //        .IgnoreResponseTypePermissions()
            //        .IgnoreScopePermissions();

            // Note: when issuing access tokens used by third-party APIs
            // you don't own, you can disable access token encryption:
            //
            // options.DisableAccessTokenEncryption();
        })

        // Register the OpenIddict validation components.
        .AddValidation(options =>
        {
            // Configure the audience accepted by this resource server.
            // The value MUST match the audience associated with the
            // "demo_api" scope, which is used by ResourceController.
            options.AddAudiences("rs_dataEventRecordsApi");

            // Import the configuration from the local OpenIddict server instance.
            options.UseLocalServer();

            // Register the ASP.NET Core host.
            options.UseAspNetCore();

            // For applications that need immediate access token or authorization
            // revocation, the database entry of the received tokens and their
            // associated authorizations can be validated for each API call.
            // Enabling these options may have a negative impact on performance.
            //
            // options.EnableAuthorizationEntryValidation();
            // options.EnableTokenEntryValidation();
        });

            //services.AddTransient<IEmailSender, AuthMessageSender>();
            //services.AddTransient<ISmsSender, AuthMessageSender>();

            //Register the worker responsible of seeding the database with the sample clients.
            // Note: in a real world application, this step should be part of a setup script.
             services.AddHostedService<Worker>();


            //services.AddSwaggerGen(c =>
            //{
            //    c.SwaggerDoc("v1", new OpenApiInfo { Title = "OIDCIndetityServer", Version = "v1" });
            //});

            

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
           /* if (env.IsDevelopment())
            {
                //app.UseDeveloperExceptionPage();
                //app.UseSwagger();
                //app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "OIDCIndetityServer v1"));
            }*/


            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseStatusCodePagesWithReExecute("~/error");
                //app.UseExceptionHandler("~/error");

                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                //app.UseHsts();
            }
            app.UseCors("AllowAllOrigins");
            app.UseStaticFiles();
            


//            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseRequestLocalization(options =>
            {
                options.AddSupportedCultures("en-US", "fr-FR");
                options.AddSupportedUICultures("en-US", "fr-FR");
                options.SetDefaultCulture("en-US");
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapDefaultControllerRoute();
                endpoints.MapRazorPages();
            });

            //app.UseEndpoints(endpoints =>
            //{
            //    endpoints.MapControllers();
            //});
        }
    }
}
