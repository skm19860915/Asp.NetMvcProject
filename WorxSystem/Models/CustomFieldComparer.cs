using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models
{
    public class CustomFieldComparer :  IEqualityComparer<CustomField>
    {
        #region IEqualityComparer<CustomField> Members

        public bool Equals(CustomField x, CustomField y)
        {
            //Check whether the compared objects reference the same data.
            if (Object.ReferenceEquals(x, y)) return true;

            //Check whether any of the compared objects is null.
            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return x.ID == y.ID;
        }

        public int GetHashCode(CustomField obj)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(obj, null)) return 0;

            return obj.ID;
        }

        #endregion
    }
}