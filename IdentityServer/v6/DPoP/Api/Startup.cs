using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ApiHost
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(
                options =>
                    options.AddDefaultPolicy(
                        policy =>
                        {
                            policy
                                .WithOrigins("http://localhost:1234")
                                .AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials()
                                .WithExposedHeaders("DPoP-Nonce");
                        }));
            
            services.AddControllers();
            

            // this API will accept any access token from the authority
            services.AddAuthentication("token")
                .AddJwtBearer("token", options =>
                {
                    options.Authority = "https://localhost:5001";
                    options.TokenValidationParameters.ValidateAudience = false;
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.ValidTypes = new[] { "at+jwt" };
                });

            // layers DPoP onto the "token" scheme above
            services.ConfigureDPoPTokensForScheme("token");
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseCors();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers().RequireAuthorization();
            });
        }
    }
}