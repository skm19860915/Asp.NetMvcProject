using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using AutoMapper;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Areas.API.Models
{
    public class APIUser
    {        
        public int ID { get; set; }        
        public string UserName { get; set; }
        public List<Role> Roles { get; set; }

        static APIUser()
        {
            //moved to global.asax.cs to to ensure only a single call
            //Mapper.CreateMap<DTO.User, APIUser>();                
            //Mapper.AssertConfigurationIsValid();
        }

        public static APIUser FromDTOUser(DTO.User fullUser)
        {
            return Mapper.Map<APIUser>(fullUser);
        }
    }
}