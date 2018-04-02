using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using DataModel;
using System.Transactions;
using Telerik.OpenAccess;
using Telerik.OpenAccess.Metadata.Fluent;

namespace CAN.Service
{
    public static class AlumniHelper
    {
         #region FacebookImport
        public static bool FillDegrees(dynamic obj, int userID)
        {
            if (obj.education == null) return false;
            var degrees = GetDegreesByUser(userID);
            bool imported = false;
            foreach (dynamic edu in obj.education)
            {
                //check if the edu record is already in
                if (edu.concentration != null &&
                    !degrees.Any(
                        p =>
                            p.College.FormalName.Equals(edu.school.name.ToString(),
                                StringComparison.InvariantCultureIgnoreCase)
                            &&
                            p.MajorType.Name.Equals(edu.concentration[0].name.ToString(),
                                StringComparison.InvariantCultureIgnoreCase)))
                {
                    if (edu.type == "College")
                    {
                        var degree = new Degree
                        {
                            CollegeID = CollegeHelper.GetCreateCollege(edu.school.name.ToString()).ID,
                            MajorTypeID = CollegeHelper.GetCreateMajorType(edu.concentration[0].name.ToString(), "").ID,
                            // todo handle cases when associate degree
                            DegreeTypeID = (int)GeneralHelper.DegreeType.Bachelor
                        };
                        if (edu.year != null)
                        {
                            degree.GraduationDate = new DateTime(Convert.ToInt32(edu.year.name.ToString()), 8, 1);
                        }
                        CreateDegree(userID, degree.DegreeTypeID, degree.CollegeID, degree.MajorTypeID, degree.MajorTypeID2,
                            degree.GraduationDate, null, null, null);
                        imported = true;
                    }
                    else if (edu.type == "Graduate School")
                    {
                        var degree = new Degree
                        {
                            CollegeID = CollegeHelper.GetCreateCollege(edu.school.name.ToString()).ID,
                            MajorTypeID = CollegeHelper.GetCreateMajorType(edu.concentration[0].name.ToString(), "").ID
                        };
                        string degreeType = edu.degree.name.ToString();
                        degree.DegreeTypeID = CollegeHelper.GetCreateDegreeType(degreeType).ID;
                        if (edu.year != null)
                        {
                            degree.GraduationDate = new DateTime(Convert.ToInt32(edu.year.name.ToString()), 8, 1);
                        }
                        CreateDegree(userID, degree.DegreeTypeID, degree.CollegeID, degree.MajorTypeID, degree.MajorTypeID2,
                            degree.GraduationDate, null, null, null);
                        imported = true;
                    }
                }
            }
   
            return imported;
        }

        public static bool FillHS(dynamic obj, int userID)
        {
            using (data db = new data())
            {
                var user = db.Users.First(p => p.UserID == userID);
                if (user.RegisteredAsAlumniOfSchoolID == null)
                {
                    if (obj.education == null)
                        return false;
                    foreach (dynamic edu in obj.education)
                    {
                        try
                        {
                            if (edu.type == "High School" && edu.year != null)
                            {
                                School schoolMatched = GeneralHelper.GetMatchingSchoolByName(edu.school.name.ToString());
                                if (schoolMatched != null)
                                {
                                    user.RegisteredAsAlumniOfSchoolID = schoolMatched.ID;
                                    user.RegisteredAsAlumniOfYear = Convert.ToInt32(edu.year.name.ToString());
                                    db.SaveChanges();
                                    return true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ex.LogException("import hs");
                        }
                    }
                }
                return false;
            }
        }

        public static bool FillWorkHistory(dynamic obj, int userID)
        {
            var emps = CorporateHelper.GetEmploymentsByUser(userID);
            if (obj.work == null)
                return false;
            bool imported = false;
            foreach (dynamic job in obj.work)
            {
                try
                {
                    string company = job.employer.name.ToString();
                    string title = job.position != null ? job.position.name.ToString() : "";
                    DateTime? startDate = job.start_date != null ? Convert.ToDateTime(job.start_date.ToString()) : null;
                    DateTime? endDate = job.end_date != null ? Convert.ToDateTime(job.end_date.ToString()) : null;

                    if (!string.IsNullOrEmpty(company) &&
                        !emps.Any(
                            p =>
                                p.Company.Equals(company, StringComparison.InvariantCultureIgnoreCase)
                                &&
                                p.Title.Name.Equals(title, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        CorporateHelper.CreateEmployment(userID, title, company, startDate, endDate);
                        imported = true;
                    }

                }
                catch (Exception ex)
                {
                    ex.LogException("work import");
                }
            }
            
            if (imported) AlumniHelper.MarkEmploymentStep(userID, true);
            return imported;
        }
       
        public static bool FillFromFB(dynamic obj, int userID)
        {
            FillHS(obj, userID);
            FillDegrees(obj, userID);
            FillWorkHistory(obj, userID);
            return true;
        }
        #endregion


}

