using System;
using Microsoft.AspNetCore.Mvc;
using ExperienceService.Models;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Experience.Infrastructure;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http.Headers;
using Swashbuckle.AspNetCore.Annotations;
using AutoMapper;
using ExperienceService.DBModels;

namespace ExperienceService.Controllers
{
    [Route("[controller]")]
    [Authorize]
    public class ClinicController : BaseController
    {
        private readonly IMapper _mapper;
        private readonly IConfiguration configuration;

        public ClinicController(IConfiguration configuration, IMapper mapper) : base(configuration)
        {
            this.configuration = configuration;
            _mapper = mapper;
        }

        [HttpGet("HealthCheck")]
        [SwaggerResponse((int)HttpStatusCode.OK, Constants.SuccessMessage, typeof(HealthCheck))]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, Constants.Badrequest, typeof(BadRequestErrors))]
        public IActionResult GetHealthCheckAsync()
        {
            try
            {
                HealthCheck healthCheck = new HealthCheck();
                var ClinicBase = configuration.GetSection("ClinicBase").Value;
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(ClinicBase);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.AccessToken);
                    var responseclinic = client.GetAsync("Clinic/HealthCheck");
                    responseclinic.Wait();
                    var clinicResult = responseclinic.Result;
                    if (clinicResult.IsSuccessStatusCode)
                    {
                        healthCheck.Status = "Healthy";
                        healthCheck.Error = "No error";
                        return Ok(healthCheck);
                    }
                    else
                    {
                        healthCheck.Status = "UnHealthy";
                        healthCheck.Error = "Clinic Domain Service is unavailable";
                        return Ok(healthCheck);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost]
        [SwaggerResponse((int)HttpStatusCode.OK, Constants.SuccessMessage, typeof(ClinicDetails))]
        [SwaggerResponse((int)HttpStatusCode.NotFound, Constants.NotFound, typeof(ClinicNotFoundForSearchError))]
        [SwaggerResponse((int)HttpStatusCode.BadRequest, Constants.Badrequest, typeof(BadRequestErrors))]
        public async Task<IActionResult> ClinicList([FromBody]ClinicFilterSort ClinicDetail)
        {
            try
            {
                var clinicBase = configuration.GetSection("ClinicBase").Value;
                var patientBase = configuration.GetSection("PatientBase").Value;

                ClinicDetail.ordering = ClinicDetail.ordering == "Z-A" ? "desc" : "asc";
                using (HttpClient client = new HttpClient())
                {
                    switch (ClinicDetail.filters.ClinicStatus)
                    {
                        case "ACTIVE":
                            ClinicDetail.filters.ClinicStatus = "Active";
                            break;
                        case "DEACTIVE":
                            ClinicDetail.filters.ClinicStatus = "Deactive";
                            break;
                        case "ALL":
                            ClinicDetail.filters.ClinicStatus = "Active, Deactive";
                            break;
                        default:
                            throw new ClinicNotFoundException(HttpStatusCode.NotFound);
                    }
                    client.BaseAddress = new Uri(clinicBase);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.AccessToken);
                    var query = "";
                    if (!string.IsNullOrEmpty(ClinicDetail.searchTerm))
                    {
                        var searchtext = ClinicDetail.searchTerm.ToLower().Replace("'", "''");
                        query = "$filter=contains('" + ClinicDetail.filters.ClinicStatus + "',status) eq true and startswith(tolower(clinicname), '" + searchtext + "') and UnsTenantId eq " + this.TenantId +" &$orderby=clinicname " + ClinicDetail.ordering + "&$count=true &$top=" + ClinicDetail.pagination.pageLimit + "&$skip=" + (ClinicDetail.pagination.pageLimit * ClinicDetail.pagination.currentPage) + "";
                    }
                    else
                    {
                        query = "$filter=contains('" + ClinicDetail.filters.ClinicStatus + "',status) eq true and UnsTenantId eq " + this.TenantId + " &$orderby=clinicname " + ClinicDetail.ordering + "&$count=true &$top=" + ClinicDetail.pagination.pageLimit + "&$skip=" + (ClinicDetail.pagination.pageLimit * ClinicDetail.pagination.currentPage) + "";
                    }
                    var responseClinic = await client.GetAsync("Clinic?" + query);
                    var clinicResult = responseClinic;
                    if (clinicResult.IsSuccessStatusCode)
                    {
                        var readClinic = await clinicResult.Content.ReadAsStringAsync();
                        var ClinicResult = JsonConvert.DeserializeObject<Clinics>(readClinic);
                        List<Clinic> ClinicList = _mapper.Map<List<UnsClinicMaster>, List<Clinic>>(ClinicResult.value);
                        List<string> ClinicIds = ClinicList.Select(x => x.clinicId).ToList();
                        using (HttpClient patientClient = new HttpClient())
                        {
                            patientClient.BaseAddress = new Uri(patientBase);
                            patientClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.AccessToken);
                            string json = JsonConvert.SerializeObject(ClinicIds);
                            StringContent sc = new StringContent(json, Encoding.UTF8, "application/json");
                            var responsePatient = await patientClient.PostAsync("PatientCount/", sc);
                            var patientResult = responsePatient;
                            if (patientResult.IsSuccessStatusCode)
                            {
                                var readPatient = await patientResult.Content.ReadAsStringAsync();
                                List<ClinicPatientCount> patients = JsonConvert.DeserializeObject<List<ClinicPatientCount>>(readPatient);
                                foreach (var clinic in ClinicList)
                                {
                                    clinic.numPatients = Convert.ToInt32(patients.Where(s => s.clinicid == clinic.clinicId).Select(s => s.numPatients).FirstOrDefault());
                                }
                                Pagination pagination = new Pagination();
                                pagination.pageLimit = ClinicDetail.pagination.pageLimit;
                                pagination.currentPage = ClinicDetail.pagination.currentPage;
                                pagination.totalRecords = ClinicResult.count;
                                ClinicPaginationList Clinics = new ClinicPaginationList();
                                Clinics.Pagination = pagination;
                                Clinics.rows = ClinicList;
                                return Ok(Clinics);
                            }
                            else
                            {
                                throw new PatientNotFoundException(HttpStatusCode.NotFound, "Clinics");
                            }
                        }
                    }
                    else
                    {
                        throw new ClinicNotFoundException(HttpStatusCode.NotFound);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}