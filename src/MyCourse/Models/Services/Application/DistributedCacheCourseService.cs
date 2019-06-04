using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyCourse.Models.Exceptions;
using MyCourse.Models.Options;
using MyCourse.Models.Services.Infrastructure;
using MyCourse.Models.ViewModels;
using Newtonsoft.Json;

namespace MyCourse.Models.Services.Application
{
    public class DistributedCacheCourseService : ICachedCourseService
    {
        private readonly ICourseService courseService;
        private readonly IDistributedCache distributedCache;
        public DistributedCacheCourseService(ICourseService courseService, IDistributedCache memoryCache)
        {
            this.courseService = courseService;
            this.distributedCache = memoryCache;
        }
        public async Task<CourseDetailViewModel> GetCourseAsync(int id)
        {
            string key = $"Course{id}";

            //Proviamo a recuperare l'oggetto dalla cache
            string serializedObject = await distributedCache.GetStringAsync(key);

            //Se otteniamo null, vuol dire che l'oggetto non era in cache
            if (serializedObject == null) {

                //Allora lo andiamo a recuperare dal database grazie all'ICourseService
                CourseDetailViewModel course = await courseService.GetCourseAsync(id);

                //Prima di restituire l'oggetto al chiamante, lo serializziamo.
                //Cioè ne creiamo una rappresentazione stringa che poi
                //potrà essere riconvertita nell'oggetto originale.
                serializedObject = Serialize(course);

                //Impostiamo la durata di permanenza in cache
                var cacheOptions = new DistributedCacheEntryOptions();
                cacheOptions.SetAbsoluteExpiration(TimeSpan.FromSeconds(60));

                //Aggiungiamo in cache l'oggetto così serializzato 
                await distributedCache.SetStringAsync(key, serializedObject, cacheOptions);
                return course;
            } else {
                return Deserialize<CourseDetailViewModel>(serializedObject);
                //return JsonConvert.DeserializeObject<CourseDetailViewModel>(cached);
            }
        }

        public async Task<List<CourseViewModel>> GetCoursesAsync()
        {
            string key = $"Courses";
            string serializedObject = await distributedCache.GetStringAsync(key);
            if (serializedObject == null) {
                List<CourseViewModel> courses = await courseService.GetCoursesAsync();
                serializedObject = Serialize(courses);
                await distributedCache.SetStringAsync(key, serializedObject);
                return courses;
            } else {
                return Deserialize<List<CourseViewModel>>(serializedObject);
            }
        }

        private string Serialize(object obj) 
        {
            //Convertiamo un oggetto in una stringa JSON
            return JsonConvert.SerializeObject(obj);
        }

        private T Deserialize<T>(string serializedObject)
        {
            //Riconvertiamo una stringa JSON in un oggetto
            return JsonConvert.DeserializeObject<T>(serializedObject);
        }
    }
}