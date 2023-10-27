using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using utility;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace EllucianAPI_Banner_Kern
{
    class Program
    {
        static void Main(string[] args)
        {
            //Check command line parameters to determine which process to run
            foreach (string value in args)
            {

                switch (value)
                {
                    case "Full":
                        FullRun();
                        break;
                    default:
                        break;
                }
            }

            //getPSOData();
            //Test(ConfigurationManager.AppSettings["base_url"].ToString(), "aca755ee-eb1b-4f6d-8c46-cbdb744de74e", "6594");
            //Test(ConfigurationManager.AppSettings["base_url"].ToString(), "32cc2d4a-75f3-4976-be67-c4e486fd1e15", "6615");
            //loopSectionsData(ConfigurationManager.AppSettings["base_url"].ToString(), "32cc2d4a-75f3-4976-be67-c4e486fd1e15", "6615");
            //saveAllSections(ConfigurationManager.AppSettings["base_url"].ToString(), "32cc2d4a-75f3-4976-be67-c4e486fd1e15", "6615", 0, "c3c2bf6f-2e19-4e99-95e6-3632a2fb1f9c");
            //FullRun();
            //Console.ReadKey();
        }
        static void FullRun()
        {
            DataTable dtSchoolIDs = new DataTable();
            //Get schools that are enabled for processing
            dtSchoolIDs = getSchoolIDs();

            if (dtSchoolIDs.Rows.Count > 0)
            {
                //loop through each school
                for (int x = 0; x < dtSchoolIDs.Rows.Count; x++)
                {
                    string base_url = ConfigurationManager.AppSettings["base_url"].ToString();
                    bool bProcessData = Convert.ToBoolean(dtSchoolIDs.Rows[x]["bProcessData"]);
                    string sintSchoolID = dtSchoolIDs.Rows[x]["sintSchoolID"].ToString();
                    string ApiKey = dtSchoolIDs.Rows[x]["vcApiKey"].ToString();
                    //string apiToken = string.Empty;
                    int offset = 0;

                    //This school is only school that uses this call. It is because all the course data for 3 different schools is under one API key, we need this to know which courses go to which school. 
                    if (sintSchoolID == "6615")
                    {
                        #region Sites Data
                        //loops through sites data
                        saveAllSites(base_url, ApiKey, sintSchoolID, offset);
                        #endregion
                    }


                    #region Term Data
                    //loops through term data
                    //saveAllTerms(base_url, ApiKey, sintSchoolID); //NOT USING THIS

                    saveAllTerms2(base_url, ApiKey, sintSchoolID, offset);
                    #endregion

                    #region Section Data
                    //loops through section data - this went back 6 months;
                    //saveAllCourses(base_url, ApiKey, sintSchoolID, offset); //NOT USING THIS

                    //get section data based on the terms we pulled
                    loopSectionsData(base_url, ApiKey, sintSchoolID);

                    loopCoursesData(base_url, ApiKey, sintSchoolID);
                    #endregion

                    #region Insturoctor Data
                    //get instructor data based on the sections we pulled
                    loopInstructorData(base_url, ApiKey, sintSchoolID);
                    #endregion


                    #region Enrollment Data
                    //loops through student data
                    loopSectionsEnrollment(base_url, ApiKey, sintSchoolID);
                    #endregion

                    #region Student Data
                    //loops through student data
                    //saveAllStudents(base_url, ApiKey, sintSchoolID, offset); // this one looped through all the students... took over an hour... // NOT USING THIS

                    //now im only pulling student info that we have enrollment info for
                    saveAllStudent2(base_url, ApiKey, sintSchoolID);
                    #endregion


                    if (bProcessData)
                    {
                        Console.WriteLine("--------Processing Course Data Into Fast------------");
                        RunProcessingSP(Convert.ToInt32(sintSchoolID), "course");
                        Console.WriteLine("--------Processing Student Data Into Fast------------");
                        RunProcessingSP(Convert.ToInt32(sintSchoolID), "student");
                        Console.WriteLine("--------Processing Schedule Data Into Fast------------");
                        RunProcessingSP(Convert.ToInt32(sintSchoolID), "schedule");

                    }


                    Console.WriteLine("------------DONE WITH " + sintSchoolID + "----------------");
                }
            }
        }
        static void RunProcessingSP(int intSchoolID, string strType)
        {
            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["FASTConnectionString"].ToString();
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings[strType + "ProcessingSP"];
                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.Int).Value = intSchoolID;

                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 0;

                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("SP:" + ConfigurationManager.AppSettings[strType + "ProcessingSP"] + "\r\n\r\n School ID: " + intSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian-Banner API Error in [[MAIN]] RunProcessingSP");
                //throw ex;
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }
        }

        private static void loopInstructorData(string baseurl, string apikey, string sintSchoolID)
        {

            DataTable dtSectionIDs = new DataTable();
            //Get all sections to loop through them
            dtSectionIDs = getSectionIDForInstructors(sintSchoolID);

            if (dtSectionIDs.Rows.Count > 0)
            {
                for (int x = 0; x < dtSectionIDs.Rows.Count; x++)
                {
                    string sectionID = dtSectionIDs.Rows[x]["sectionID"].ToString();
                    saveAllInstructors(baseurl, apikey, sintSchoolID, 0, sectionID);

                }
            }



        }

        private static void loopCoursesData(string baseurl, string apikey, string sintSchoolID)
        {

            DataTable dtCourseIDs = new DataTable();
            //Get all sections to loop through them
            dtCourseIDs = getCourseIDsFromSections(sintSchoolID);

            if (dtCourseIDs.Rows.Count > 0)
            {
                for (int x = 0; x < dtCourseIDs.Rows.Count; x++)
                {
                    //string sectionID = dtSectionIDs.Rows[x]["sectionID"].ToString();
                    string courseID = dtCourseIDs.Rows[x]["courseID"].ToString();
                    saveAllCourses(baseurl, apikey, sintSchoolID, 0, courseID);

                }
            }



        }

        private static void loopSectionsData(string baseurl, string apikey, string sintSchoolID)
        {

            DataTable dtSectionIDs = new DataTable();
            //Get all sections to loop through them
            dtSectionIDs = getTermIDForEnrollment(sintSchoolID);

            if (dtSectionIDs.Rows.Count > 0)
            {
                for (int x = 0; x < dtSectionIDs.Rows.Count; x++)
                {
                    //string sectionID = dtSectionIDs.Rows[x]["sectionID"].ToString();
                    string termID = dtSectionIDs.Rows[x]["id"].ToString();
                    saveAllSections(baseurl, apikey, sintSchoolID, 0, termID);

                }
            }



        }

        private static void loopSectionsEnrollment(string baseurl, string apikey, string sintSchoolID)
        {

            DataTable dtSectionIDs = new DataTable();
            //Get all sections to loop through them
            dtSectionIDs = getTermIDForEnrollment(sintSchoolID);

            if (dtSectionIDs.Rows.Count > 0)
            {
                for (int x = 0; x < dtSectionIDs.Rows.Count; x++)
                {
                    //string sectionID = dtSectionIDs.Rows[x]["sectionID"].ToString();
                    string termID = dtSectionIDs.Rows[x]["id"].ToString(); ;
                    saveSectionRegistrations(baseurl, apikey, sintSchoolID, 0, termID);

                }
            }



        }

        private static void saveSectionRegistrations(string baseurl, string apikey, string sintSchoolID, int offset, string termID)
        {



            try
            {
                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                string strHtml = string.Empty;
                strHtml = getEnrollmentData(baseurl, apitoken, sintSchoolID, offset, termID);

                dynamic EnrollmentData = JsonConvert.DeserializeObject(strHtml);

                foreach (var enrollment in EnrollmentData)
                {

                    string section = string.Empty;
                    string studentID = string.Empty;
                    string registrationStatus = string.Empty;
                    string sectionRegistrationStatusReason = string.Empty;

                    if (enrollment.section != null)
                    {
                        section = enrollment.section.id;
                    }
                    if (enrollment.registrant != null)
                    {
                        studentID = enrollment.registrant.id;
                    }
                    if (enrollment.status != null)
                    {
                        registrationStatus = enrollment.status.registrationStatus;
                        sectionRegistrationStatusReason = enrollment.status.sectionRegistrationStatusReason;
                    }

                    Console.WriteLine("Section ID: " + section);
                    Console.WriteLine("Student ID: " + studentID);
                    Console.WriteLine("registrationStatus: " + registrationStatus);
                    Console.WriteLine("sectionRegistrationStatusReason: " + sectionRegistrationStatusReason);
                    Console.WriteLine("------------------------");

                    saveEnrollmentData(sintSchoolID, section, studentID, registrationStatus, sectionRegistrationStatusReason);

                }


                if (EnrollmentData.Count == 500)
                {
                    offset = offset + 500;
                    saveSectionRegistrations(baseurl, apitoken, sintSchoolID, offset, termID);
                }


            }
            catch (Exception ex)
            {
                sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveSectionRegistrations");
                //Console.WriteLine(ex.ToString());
            }




        }
        private static void saveAllStudent2(string baseurl, string apikey, string sintSchoolID)
        {

            DataTable dtStudentIDs = new DataTable();
            //Get schools that are enabled for processing
            dtStudentIDs = getStudentIDs(sintSchoolID);

            if (dtStudentIDs.Rows.Count > 0)
            {
                for (int x = 0; x < dtStudentIDs.Rows.Count; x++)
                {
                    string studentID = dtStudentIDs.Rows[x]["studentID"].ToString();

                    try
                    {
                        string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                        string strHtml = string.Empty;
                        string strHtml2 = string.Empty;
                        strHtml = getStudentData2(baseurl, apitoken, sintSchoolID, studentID);

                        dynamic StudentData = JsonConvert.DeserializeObject(strHtml);

                        // Console.WriteLine(StudentData);
                        //Console.ReadKey();


                        string id = string.Empty;
                        string email = string.Empty;
                        string hrEmail = string.Empty;
                        string schoolEmail = string.Empty;
                        string FullName = string.Empty;
                        string studentsId = string.Empty;
                        string studentsId2 = string.Empty;
                        string address = string.Empty;
                        string city = string.Empty;
                        string state = string.Empty;
                        string zip = string.Empty;
                        string isPSOStudent = string.Empty;

                        //Console.WriteLine(student);
                        if (StudentData.id != null)
                        {
                            id = StudentData.id;
                        }
                        if (StudentData.names != null)
                        {
                            FullName = StudentData.names[0].fullName;
                        }
                        if (StudentData.emails != null)
                        {

                            if (sintSchoolID == "6615")
                            {
                                foreach (var emails2 in StudentData.emails)
                                {
                                    if (emails2.type.emailType == "school")
                                    {
                                        schoolEmail = emails2.address;
                                    }
                                    if (emails2.type.emailType == "hr")
                                    {
                                        hrEmail = emails2.address;
                                    }
                                }
                                if (hrEmail != string.Empty)
                                {
                                    email = hrEmail;
                                }
                                else if (hrEmail == string.Empty && schoolEmail != string.Empty)
                                {
                                    email = schoolEmail;
                                }
                                //if cant find school/hr email, just grab the first one in the array.
                                if (email == string.Empty)
                                {
                                    email = StudentData.emails[0].address;
                                }

                            }
                            else
                            {
                                foreach (var emails2 in StudentData.emails)
                                {
                                    if (emails2.type.emailType == "school")
                                    {
                                        email = emails2.address;
                                    }
                                }
                                //if cant find school email, just grab the first one in the array.
                                if (email == string.Empty)
                                {
                                    email = StudentData.emails[0].address;
                                }
                            }


                        }
                        if (StudentData.studentsId != null)
                        {
                            studentsId = StudentData.studentsId.studentsId;
                        }
                        //if student ID is blank, try and look at this other location
                        if (studentsId == string.Empty)
                        {
                            if (StudentData.credentials != null)
                            {
                                foreach (var cred in StudentData.credentials)
                                {
                                    if (cred.type == "bannerSourcedId")
                                    {
                                        studentsId = cred.value;
                                    }
                                    else if (cred.type == "bannerId")
                                    {
                                        studentsId2 = cred.value;
                                    }
                                }
                            }


                        }

                        if (StudentData.addressLines != null)
                        {
                            address = StudentData.addressLines;
                        }
                        if (StudentData.city != null)
                        {
                            city = StudentData.city;
                        }
                        if (StudentData.state != null)
                        {
                            state = StudentData.state;
                        }
                        if (StudentData.zip != null)
                        {
                            zip = StudentData.zip;
                        }

                        if (sintSchoolID == "6594")
                        {

                            strHtml2 = getPSOStudent(baseurl, apitoken, sintSchoolID, studentsId2);
                            dynamic rateCode = JsonConvert.DeserializeObject(strHtml2);
                            if (rateCode != null)
                            {
                                if (rateCode[0].SGBSTDN[0].rateCode != null)
                                    isPSOStudent = rateCode[0].SGBSTDN[0].rateCode;
                            }

                        }

                        saveStudentData(sintSchoolID, id, FullName, email, studentsId, studentsId2, address, city, state, zip, isPSOStudent);
                        Console.WriteLine("Saving student: " + studentsId);
                        Console.WriteLine(id);
                        Console.WriteLine(email);
                        Console.WriteLine(address);
                        Console.WriteLine(city);
                        Console.WriteLine(state);
                        Console.WriteLine(zip);
                        Console.WriteLine("----------");

                        //Console.ReadKey();





                    }
                    catch (Exception ex)
                    {
                        sendEmail("School ID: " + sintSchoolID + "\r\nBase Url: " + baseurl + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllStudent2");
                        //Console.WriteLine(ex.ToString());
                    }




                }
            }



        }


        private static void saveAllTerms(string baseurl, string apikey, string sintSchoolID)
        {

            DataTable dtTermIDs = new DataTable();
            //Get schools that are enabled for processing
            dtTermIDs = getTermIDs(sintSchoolID);

            if (dtTermIDs.Rows.Count > 0)
            {
                for (int x = 0; x < dtTermIDs.Rows.Count; x++)
                {
                    string academicPeriod = dtTermIDs.Rows[x]["academicPeriod"].ToString();


                    try
                    {
                        string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                        string strHtml = string.Empty;
                        strHtml = getTermData(baseurl, apitoken, sintSchoolID, academicPeriod);

                        dynamic TermData = JsonConvert.DeserializeObject(strHtml);
                        string id = string.Empty;
                        string title = string.Empty;
                        string code = string.Empty;
                        string startOn = string.Empty;
                        string endOn = string.Empty;

                        if (TermData.id != null)
                        {
                            id = TermData.id;
                        }
                        if (TermData.title != null)
                        {
                            title = TermData.title;
                        }
                        if (TermData.code != null)
                        {
                            code = TermData.code;
                        }
                        if (TermData.startOn != null)
                        {
                            startOn = TermData.startOn;
                        }
                        if (TermData.endOn != null)
                        {
                            endOn = TermData.endOn;
                        }

                        //saveTermData(sintSchoolID, id, title, startOn, endOn, code);

                        Console.WriteLine("Saving term ID: " + id);
                        Console.WriteLine(title);
                        Console.WriteLine(code);
                        Console.WriteLine(startOn);
                        Console.WriteLine(endOn);
                        Console.WriteLine("-------------------------");


                    }
                    catch (Exception ex)
                    {
                        sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllTerms");
                        //Console.WriteLine(ex.ToString());
                    }


                }
            }



        }
        private static void Test(string baseurl, string apikey, string sintSchoolID)
        {
            try
            {
                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);

                string strHtml = string.Empty;
                //string email = string.Empty;
                //string hrEmail = string.Empty;
                //string schoolEmail = string.Empty;

                strHtml = getTestData(baseurl, apitoken, sintSchoolID);

                dynamic TestData = JsonConvert.DeserializeObject(strHtml);


                /*
                if (TestData[0].SGBSTDN2[0] != null)
                {
                    Console.WriteLine(TestData[0].SGBSTDN[0].rateCode);
                }*/

                /*
                Console.WriteLine(TestData.maxEnrollment);
                if (TestData.zeroTextbookCost != null)
                {
                    Console.WriteLine(TestData.zeroTextbookCost);
                } else
                {
                    Console.WriteLine("not here");
                }
                
                /*
                foreach (var test in TestData)
                {
                    Console.WriteLine(test);
                    Console.ReadKey();
                }
                */


                /*
                using (StreamWriter writer = new StreamWriter(@"C:\Users\lbarnes\Desktop\test\ellucian.txt"))
                {
                    writer.WriteLine(TestData);
                }
                Console.WriteLine("done");*/





            }
            catch (Exception ex)
            {
                //sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Banner Error in [[MAIN]] Test");
                Console.WriteLine(ex.ToString());
            }


        }



        private static void saveAllSites(string baseurl, string apikey, string sintSchoolID, int offset)
        {
            try
            {
                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                string strHtml = string.Empty;
                strHtml = getSiteData(baseurl, apitoken, sintSchoolID, offset);

                dynamic SiteData = JsonConvert.DeserializeObject(strHtml);

                foreach (var sites in SiteData)
                {
                    //Console.WriteLine(sites);
                    //Console.ReadKey();

                    string id = string.Empty;
                    string title = string.Empty;
                    string code = string.Empty;


                    if (sites.id != null)
                    {
                        id = sites.id;
                    }
                    if (sites.title != null)
                    {
                        title = sites.title;
                    }
                    if (sites.code != null)
                    {
                        code = sites.code;
                    }

                    Console.WriteLine("Saving: " + code);


                    saveSitesData(sintSchoolID, id, title, code);



                }

                if (SiteData.Count == 100)
                {
                    offset = offset + 100;
                    saveAllSites(baseurl, apitoken, sintSchoolID, offset);
                }

            }
            catch (Exception ex)
            {
                sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllSites");
                //Console.WriteLine(ex.ToString());
            }


        }


        private static void saveAllTerms2(string baseurl, string apikey, string sintSchoolID, int offset)
        {
            try
            {
                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                string strHtml = string.Empty;
                strHtml = getTermData2(baseurl, apitoken, sintSchoolID, offset);

                dynamic TermData2 = JsonConvert.DeserializeObject(strHtml);

                foreach (var TermData in TermData2)
                {
                    //Console.WriteLine(TermData);
                    //Console.ReadKey();
                    string id = string.Empty;
                    string title = string.Empty;
                    string code = string.Empty;
                    string startOn = string.Empty;
                    string endOn = string.Empty;
                    string parent_id = string.Empty;
                    string category_type = string.Empty;

                    if (TermData.id != null)
                    {
                        id = TermData.id;
                    }

                    if (TermData.title != null)
                    {
                        title = TermData.title;
                    }
                    if (TermData.code != null)
                    {
                        code = TermData.code;
                    }
                    if (TermData.startOn != null)
                    {
                        startOn = TermData.startOn;
                    }
                    if (TermData.endOn != null)
                    {
                        endOn = TermData.endOn;
                    }
                    if (TermData.category.parent != null)
                    {
                        parent_id = TermData.category.parent.id;
                    }
                    if (TermData.category != null)
                    {
                        category_type = TermData.category.type;
                    }

                    saveTermData(sintSchoolID, id, title, startOn, endOn, code, parent_id, category_type);

                    Console.WriteLine("Saving term ID: " + id);
                    Console.WriteLine(title);
                    Console.WriteLine(code);
                    Console.WriteLine(startOn);
                    Console.WriteLine(endOn);
                    Console.WriteLine(parent_id);
                    Console.WriteLine(category_type);
                    Console.WriteLine("-------------------------");

                }

                if (TermData2.Count == 100)
                {
                    offset = offset + 100;
                    saveAllTerms2(baseurl, apitoken, sintSchoolID, offset);
                }

            }
            catch (Exception ex)
            {
                sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllTerms2");
                //Console.WriteLine(ex.ToString());
            }


        }

        private static void saveAllInstructors(string baseurl, string apikey, string sintSchoolID, int offset, string sectionID)
        {

            try
            {

                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                string strHtml = string.Empty;
                string strHtml2 = string.Empty;
                //sectionID = "003ad7dd-0af5-485b-946f-d3e6ee6f2fc1";
                strHtml = getInstructorData(baseurl, apitoken, sintSchoolID, offset, sectionID);

                dynamic InstructorData = JsonConvert.DeserializeObject(strHtml);

                HashSet<string> uniqueStrings = new HashSet<string>();

                foreach (var instructor in InstructorData)
                {
                    string instructorID = string.Empty;

                    instructorID = instructor.instructor.id;

                    //they will list the same instructor multiple times for some reason. So i just want a list of uniquie instructors
                    uniqueStrings.Add(instructorID);

                }
                if (uniqueStrings.Count > 0)
                {
                    foreach (var item in uniqueStrings)
                    {
                        //Console.WriteLine(item); 
                        //item is insturoctor ID
                        strHtml2 = getInstructorPersonalData(baseurl, apitoken, sintSchoolID, offset, item);
                        dynamic InstructorPersonalData = JsonConvert.DeserializeObject(strHtml2);
                        Console.WriteLine("--------------------------");
                        Console.WriteLine("Data for: " + item);
                        //Console.WriteLine(InstructorPersonalData);
                        string FullName = string.Empty;
                        string email = string.Empty;
                        string hrEmail = string.Empty;
                        string schoolEmail = string.Empty;


                        if (InstructorPersonalData != null)
                        {
                            if (InstructorPersonalData.names != null)
                            {
                                FullName = InstructorPersonalData.names[0].fullName;
                            }
                            if (InstructorPersonalData.emails != null)
                            {

                                foreach (var emails2 in InstructorPersonalData.emails)
                                {
                                    if (emails2.type.emailType == "school")
                                    {
                                        schoolEmail = emails2.address;
                                    }
                                    if (emails2.type.emailType == "hr")
                                    {
                                        if (sintSchoolID == "6615") //kern has a stupid amount of emails in there system, and its damn near impossible to pick the right one.
                                        {
                                            string kern = emails2.address;
                                            if (kern.Contains("bakersfieldcollege.edu") || kern.Contains("portervillecollege.edu") || kern.Contains("cerrocoso.edu"))
                                            {
                                                hrEmail = emails2.address;
                                            }
                                        }
                                        else
                                        {
                                            hrEmail = emails2.address;
                                        }
                                    }
                                }
                                if (hrEmail != string.Empty)
                                {
                                    email = hrEmail;
                                }
                                else if (hrEmail == string.Empty && schoolEmail != string.Empty)
                                {
                                    email = schoolEmail;
                                }

                                //if cant find school/hr email, just grab the first one in the array.
                                if (email == string.Empty)
                                {
                                    email = InstructorPersonalData.emails[0].address;
                                }

                            }
                            Console.WriteLine(FullName);
                            Console.WriteLine(email);
                            saveInstructorData(sintSchoolID, sectionID, item, FullName, email);
                            //Console.ReadKey();
                        }


                    }
                }


                /*
                if (InstructorData.Count == 100)
                {
                    offset = offset + 100;
                    saveAllInstructors(baseurl, apitoken, sintSchoolID, offset, sectionID);
                }*/

            }
            catch (Exception ex)
            {
                sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllInstructors");
                //Console.WriteLine(ex.ToString());
            }


        }

        private static void saveAllCourses(string baseurl, string apikey, string sintSchoolID, int offset, string courseID)
        {

            try
            {
                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                string strHtml = string.Empty;

                string strSubjectData = string.Empty;

                strHtml = getCourseData(baseurl, apitoken, sintSchoolID, offset, courseID);

                dynamic CourseData = JsonConvert.DeserializeObject(strHtml);

                string title = string.Empty;
                string title2 = string.Empty;
                string subjectID = string.Empty;
                string courseNumber = string.Empty;
                string id = string.Empty;
                string subject_short = string.Empty;
                string subject = string.Empty;
                string creditHours = string.Empty;

                if (CourseData.id != null)
                {
                    id = CourseData.id;
                }
                if (CourseData.subject.id != null)
                {
                    subjectID = CourseData.subject.id;
                }
                if (CourseData.number != null)
                {
                    courseNumber = CourseData.number;
                }
                if (CourseData.titles[0].value != null)
                {
                    title = CourseData.titles[0].value;
                }
                if (CourseData.titles.Count >= 2)
                {
                    if (CourseData.titles[1].value != null)
                    {
                        title2 = CourseData.titles[1].value;
                    }
                }
                if (CourseData.credits != null)
                {
                    if (CourseData.credits[0].maximum != null)
                    {
                        creditHours = CourseData.credits[0].maximum;
                    }
                    else
                    {
                        creditHours = CourseData.credits[0].minimum;
                    }

                }


                //get subject info
                if (subjectID != string.Empty)
                {
                    strSubjectData = getSubjectData(baseurl, apitoken, sintSchoolID, subjectID);
                    dynamic SubjectData = JsonConvert.DeserializeObject(strSubjectData);
                    subject_short = SubjectData.abbreviation;
                    subject = SubjectData.title;

                }

                saveCourseData(sintSchoolID, courseID, subjectID, title, title2, courseNumber, subject, subject_short, creditHours);

                Console.WriteLine(id);
                Console.WriteLine(subjectID);
                Console.WriteLine(courseNumber);
                Console.WriteLine(title);
                Console.WriteLine(title2);
                Console.WriteLine(subject_short);
                Console.WriteLine(subject);
                Console.WriteLine("-----------------------------");
                //Console.ReadKey();



            }
            catch (Exception ex)
            {
                sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllCourses");
                //Console.WriteLine(ex.ToString());
            }


        }


        private static void saveAllSections(string baseurl, string apikey, string sintSchoolID, int offset, string termID)
        {

            try
            {
                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                string strHtml = string.Empty;
                string strHtml2 = string.Empty;

                strHtml = getSectionData(baseurl, apitoken, sintSchoolID, offset, termID);

                dynamic CourseData = JsonConvert.DeserializeObject(strHtml);
                //Console.WriteLine(CourseData);
                Console.WriteLine(CourseData.Count);
                //Console.ReadKey();


                foreach (var course in CourseData)
                {

                    //Console.WriteLine(course);
                    //Console.ReadKey();
                    string courseID = string.Empty;
                    string sectionID = string.Empty;
                    string title = string.Empty;
                    string startOn = string.Empty;
                    string endOn = string.Empty;
                    string code = string.Empty;
                    string number = string.Empty;
                    string maxEnrollment = string.Empty;
                    string academicPeriod = string.Empty;
                    string vcLevel1 = string.Empty;
                    string vcLevel2 = string.Empty;
                    string vcLevel3 = string.Empty;
                    string creditHours = string.Empty;
                    string status = string.Empty;
                    string site_id = string.Empty;
                    string zeroTextbookCost = string.Empty;

                    if (course.id != null)
                    {
                        sectionID = course.id;
                    }
                    if (course.course != null)
                    {
                        courseID = course.course.id;
                    }
                    if (course.academicPeriod != null)
                    {
                        academicPeriod = course.academicPeriod.id;
                    }
                    if (course.startOn != null)
                    {
                        startOn = course.startOn;
                    }
                    if (course.endOn != null)
                    {
                        endOn = course.endOn;
                    }
                    if (course.code != null)
                    {
                        code = course.code;
                        if (code.Contains('-'))
                        {
                            string[] levels = code.Split('-');
                            vcLevel1 = levels[0];
                            vcLevel2 = levels[1];
                            vcLevel3 = levels[2];
                        }

                    }
                    if (course.number != null)
                    {
                        number = course.number;
                    }
                    if (course.maxEnrollment != null)
                    {
                        maxEnrollment = course.maxEnrollment;
                    }
                    if (course.titles != null)
                    {
                        title = course.titles[0].value;
                    }
                    if (course.credits != null)
                    {
                        creditHours = course.credits[0].minimum;
                    }
                    if (course.status != null)
                    {
                        status = course.status.category;
                    }
                    if (course.site != null)
                    {
                        site_id = course.site.id;
                    }
                    if (course.zeroTextbookCost != null)
                    {
                        zeroTextbookCost = course.zeroTextbookCost;
                    }
                    if (sintSchoolID == "6615")
                    {

                        strHtml2 = getZeroCostField(baseurl, apitoken, sintSchoolID, sectionID);
                        dynamic sectionData2 = JsonConvert.DeserializeObject(strHtml2);
                        if (sectionData2.zeroTextbookCost != null)
                        {
                            zeroTextbookCost = sectionData2.zeroTextbookCost;
                        }

                    }




                    saveSectionData(sintSchoolID, sectionID, courseID, title, startOn, endOn, code, vcLevel1, vcLevel2, vcLevel3, number, maxEnrollment, academicPeriod, creditHours, status, site_id, zeroTextbookCost);

                    Console.WriteLine("Saving section: " + sectionID);
                    Console.WriteLine("Course ID: " + courseID);
                    Console.WriteLine(title);
                    Console.WriteLine(startOn);
                    Console.WriteLine(endOn);
                    Console.WriteLine(code);
                    Console.WriteLine("vcLevel1: " + vcLevel1);
                    Console.WriteLine("vcLevel2: " + vcLevel2);
                    Console.WriteLine("vcLevel3: " + vcLevel3);
                    Console.WriteLine(number);
                    Console.WriteLine(maxEnrollment);
                    Console.WriteLine(academicPeriod);
                    Console.WriteLine("creditHours: " + creditHours);
                    Console.WriteLine("status: " + status);
                    Console.WriteLine("--------------------------");
                    //Console.ReadKey();

                }

                if (CourseData.Count == 75)
                {
                    offset = offset + 75;
                    saveAllSections(baseurl, apitoken, sintSchoolID, offset, termID);
                }

            }
            catch (Exception ex)
            {
                sendEmail("School ID: " + sintSchoolID + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllSections");
                //Console.WriteLine(ex.ToString());
            }


        }
        public static string getPSOData(string baseurl, string apitoken, string sintSchoolID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                baseurl = baseurl + "/api/general-student-learner-and-curricula?criteria={\"id\":\"100416870\"}";

                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.v11+json";

                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            */
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }
        public static string getZeroCostField(string baseurl, string apitoken, string sintSchoolID, string id)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                baseurl = baseurl + "/api/sections/" + id;
               // Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Headers.Add("Limit", "1");
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.sections-maximum.v16.0.0+json";
                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            /*
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getZeroCostField()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;

            }*/
            
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getZeroCostField()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }
        public static string getPSOStudent(string baseurl, string apitoken, string sintSchoolID, string studentID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                baseurl = baseurl + "/api/general-student-learner-and-curricula?criteria={\"id\":\"" + studentID + "\"}";

                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.v16.0.0+json";

                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getPSOStudent()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            */
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getTestData(string baseurl, string apitoken, string sintSchoolID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {
                //this one gets all terms, even in the future
                //baseurl = baseurl + "/api/courses/cea0e96d-f51a-417a-a8a4-b53126cccbd2";
                //baseurl = baseurl + "/api/subjects/bee59888-b63f-4aec-ad76-56bfd8b205da";
                //baseurl = baseurl + "/api/course-categories/1d7013ec-d4cc-4a70-9a92-802ec3aeb5e0";
                //baseurl = baseurl + "/api/academic-periods";
                //baseurl = baseurl + "/api/persons/1ae88dda-753c-484f-b8ae-21d12e16d70b";
                //baseurl = baseurl + "/api/restricted-student-financial-aid-awards?criteria={\"awardFund\": {\"id\": \"5fe4b259-d4d5-4477-a674-32fe61337d1c\"},\"aidYear\":{\"id\":\"ab6b3860-3256-4eea-915f-4b48adcbb963\"}}";
                //Console.WriteLine(apitoken);
                baseurl = baseurl + "/api/general-student-learner-and-curricula?criteria={\"id\":\"100398081\"}";

                // baseurl = baseurl + "/api/persons/fffe69e5-d2f1-4b69-9c6e-be7e05f9abc1";
                //baseurl = baseurl + "/api/sections/cceb33a5-3dbc-42fe-8f41-3cde90aa197b";
                //baseurl = baseurl + "/api/sections?criteria={\"code\":\"70035\"}";
                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                //myRequest.Headers.Add("Limit", "1");
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.v16.0.0+json";
                //myRequest.Accept = "application/vnd.hedtech.integration.v7+json";
                //myRequest.Accept = "application/vnd.hedtech.integration.v11+json";
                //myRequest.Accept = "application/vnd.hedtech.integration.v1.0.0+json";
                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            */
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }



        public static string getSiteData(string baseurl, string apitoken, string sintSchoolID, int offset)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {


                baseurl = baseurl + "/api/sites?offset=" + offset;

                Console.WriteLine(baseurl);
                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Accept = "application/vnd.hedtech.integration.v6+json";
                myRequest.Method = "GET";
                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getSiteData()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            */
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getTermData2(string baseurl, string apitoken, string sintSchoolID, int offset)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {
                //this one gets all terms, even in the future
                baseurl = baseurl + "/api/academic-periods?criteria={\"startOn\":{\"$gte\":\"" + DateTime.Today.AddMonths(-12).ToString("yyyy-MM-dd") + "T00:00:00Z\"}, \"endOn\":{\"$lte\":\"" + DateTime.Today.AddMonths(+12).ToString("yyyy-MM-dd") + "T00:00:00Z\"}}&offset=" + offset;

                //baseurl = baseurl + "/api/academic-periods?criteria={\"startOn\":{\"$gte\":\"" + DateTime.Today.AddMonths(-6).ToString("yyyy-MM-dd") + "T00:00:00Z\"}, \"endOn\":{\"$lte\":\"" + DateTime.Today.AddMonths(+12).ToString("yyyy-MM-dd") + "T00:00:00Z\"}, \"category\":{\"type\":\"term\"}}&offset=" + offset;

                // baseurl = baseurl + "/api/academic-periods/65e91f27-8f9f-4c1e-b1f4-6dceb3e9f3d6";
                //this only get terms that are open for registration
                //baseurl = baseurl + "/api/academic-periods?criteria={\"startOn\":{\"$gte\":\"" + DateTime.Today.AddMonths(-6).ToString("yyyy-MM-dd") + "\"},\"registration\":\"open\"}&offset=" + offset;
                Console.WriteLine(baseurl);
                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Accept = "application/vnd.hedtech.integration.v16+json";
                myRequest.Method = "GET";
                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getTermData2()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            */
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getTermData(string baseurl, string apitoken, string sintSchoolID, string academicPeriod)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {
                baseurl = baseurl + "/api/academic-periods/" + academicPeriod;

                //Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Method = "GET";
                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in geTermData()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getEnrollmentData(string baseurl, string apitoken, string sintSchoolID, int offset, string sectionID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {
                //baseurl = baseurl + "/api/section-registrations?criteria={\"section\":{\"id\":\""+ sectionID + "\"}}&offset=" + offset;
                baseurl = baseurl + "/api/section-registrations?academicPeriod={\"academicPeriod\":{\"id\":\"" + sectionID + "\"}}&limit=500&offset=" + offset;
                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.v16+json";
                myRequest.Timeout = 20000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getEnrollmentData()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getEnrollmentData()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            */

            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getInstructorPersonalData(string baseurl, string apitoken, string sintSchoolID, int offset, string instructorID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {
                //baseurl = baseurl + "/api/section-instructors?criteria={\"section\":{\"id\":\"" + sectionID + "\"}}";
                baseurl = baseurl + "/api/persons/" + instructorID;
                //Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.v12+json";
                myRequest.Timeout = 40000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (Exception ex)
            {
                if (!ex.ToString().Contains("Not Found"))
                {
                    sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getInstructorPersonalData()");
                }
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }


        public static string getInstructorData(string baseurl, string apitoken, string sintSchoolID, int offset, string sectionID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {
                baseurl = baseurl + "/api/section-instructors?criteria={\"section\":{\"id\":\"" + sectionID + "\"}}";
                //baseurl = baseurl + "/api/instructors/f6242cdb-92f9-483a-82b1-d779f82d5f86";

                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.v10+json";
                myRequest.Timeout = 40000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getInstructorData()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {

                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getInstructorData()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }
            */
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getSectionData(string baseurl, string apitoken, string sintSchoolID, int offset, string termID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {
                //baseurl = baseurl + "/api/section-instructors?criteria={\"section\":{\"id\":\"3b3d3665-dd51-4b75-abee-14e5ca4af0e1\"}}";
                //baseurl = baseurl + "/api/instructors/f6242cdb-92f9-483a-82b1-d779f82d5f86";
                //get courses that have a start date going back one year
                //baseurl = baseurl + "/api/sections?criteria={\"startOn\":\""+ DateTime.Today.AddYears(-1).ToString("yyyy-MM-dd") + "\"}&limit=4";
                //baseurl = baseurl + "/api/sections?criteria={\"startOn\":\"" + DateTime.Today.AddMonths(-12).ToString("yyyy-MM-dd") + "\",\"status\":{\"category\":\"open\"}}&offset=" + offset;

                baseurl = baseurl + "/api/sections?criteria={\"academicPeriod\":{\"id\":\"" + termID + "\"}}&offset=" + offset + "&limit=75";
                //baseurl = baseurl + "/api/sections?criteria={\"academicPeriod\":{\"id\":\"415c4914-3b9d-4682-a9ea-6a0cb01ec44b\"}}&offset=900";
                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
  
                myRequest.Accept = "application/vnd.hedtech.integration.v16+json";
                //myRequest.Accept = "application/vnd.hedtech.integration.sections-maximum.v16.0.0+json";
                myRequest.Method = "GET";
                myRequest.Timeout = 400000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    // Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getSectionData()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {
                
                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getCourseData()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }*/
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }
        public static string getSubjectData(string baseurl, string apitoken, string sintSchoolID, string subjectID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                baseurl = baseurl + "/api/subjects/" + subjectID;

                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Accept = "application/vnd.hedtech.integration.v6+json";
                myRequest.Method = "GET";
                myRequest.Timeout = 400000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    // Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getSubjectData()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {
                
                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getCourseData()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }*/
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getCourseData(string baseurl, string apitoken, string sintSchoolID, int offset, string courseID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                baseurl = baseurl + "/api/courses/" + courseID;

                Console.WriteLine(baseurl);

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                myRequest.Accept = "application/vnd.hedtech.integration.v16+json";
                myRequest.Method = "GET";
                myRequest.Timeout = 400000;
                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    // Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getSectionData()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {
                
                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getCourseData()");
                //Console.WriteLine(ex.Message);
                strHTML = string.Empty;
            }*/
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        private static void saveAllStudents(string baseurl, string apikey, string sintSchoolID, int offset)
        {
            /*
            try
            {
                string apitoken = GetApiToken(baseurl, apikey, sintSchoolID);
                string strHtml = string.Empty;
                strHtml = getStudentData(baseurl, apitoken, sintSchoolID, offset);

                dynamic StudentData = JsonConvert.DeserializeObject(strHtml);

                //Console.WriteLine(StudentData);
                //Console.ReadKey();



                foreach (var student in StudentData)
                {
                    string id = string.Empty;
                    string email = string.Empty;
                    string FullName = string.Empty;
                    string studentsId = string.Empty;
                    string address = string.Empty;
                    string city = string.Empty;
                    string state = string.Empty;
                    string zip = string.Empty;

                    //Console.WriteLine(student);
                    if (student.id != null)
                    {
                        id = student.id;
                    }
                    if (student.names != null)
                    {
                        FullName = student.names[0].fullName;
                    }
                    if (student.emails != null)
                    {
                        email = student.emails[0].address;
                    }
                    if (student.studentsId != null)
                    {
                        studentsId = student.studentsId.studentsId;
                    }
                    if (student.addressLines != null)
                    {
                        address = student.addressLines;
                    }
                    if (student.city != null)
                    {
                        city = student.city;
                    }
                    if (student.state != null)
                    {
                        state = student.state;
                    }
                    if (student.zip != null)
                    {
                        zip = student.zip;
                    }
                    saveStudentData(sintSchoolID, id, FullName, email, studentsId, address, city, state, zip);
                    Console.WriteLine("Saving student: " + studentsId);
                    Console.WriteLine(id);
                    Console.WriteLine(email);
                    Console.WriteLine(address);
                    Console.WriteLine(city);
                    Console.WriteLine(state);
                    Console.WriteLine(zip);
                    Console.WriteLine("----------");
                    //Console.ReadKey();

                }

                if (StudentData.Count == 100)
                {
                    offset = offset + 100;
                    saveAllStudents(baseurl, apitoken, sintSchoolID, offset);
                }

            }
            catch (Exception ex)
            {
                sendEmail("School ID: " + sintSchoolID + "\r\nBase Url: " + baseurl + "\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in [[MAIN]] saveAllStudents");
                //Console.WriteLine(ex.ToString());
            }
            */

        }
        public static string getStudentData(string baseurl, string apitoken, string sintSchoolID, int offset)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                //baseurl = baseurl + "/api/persons?criteria={\"roles\":[{\"role\":\"student\"}]}&offset=" + offset;
                //baseurl = baseurl + "/api/persons/077da734-85e6-4bd0-8a1e-ddfc04a43e9b";
                //baseurl = baseurl + "/api/students/78c5a7b9-e8da-4b1f-8205-605b5c10f493";
                Console.WriteLine(baseurl);
                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                //myRequest.ContentType = "application/json";
                myRequest.Method = "GET";
                // myRequest.ContentLength = 0;
                myRequest.Timeout = 20000;

                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);

                strHTML = sr.ReadToEnd();
                sr.Close();
                myResponse.Close();
            }
            catch (Exception ex)
            {
                //sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getStudents()");
                Console.WriteLine(ex.ToString());
                strHTML = string.Empty;
            }
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        public static string getStudentData2(string baseurl, string apitoken, string sintSchoolID, string studentID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                //baseurl = baseurl + "/api/persons?criteria={\"roles\":[{\"role\":\"student\"}]}&offset=" + offset;
                baseurl = baseurl + "/api/persons/" + studentID;
                //baseurl = baseurl + "/api/students/78c5a7b9-e8da-4b1f-8205-605b5c10f493";
                //Console.WriteLine(baseurl);
                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apitoken);
                //myRequest.ContentType = "application/json";
                myRequest.Method = "GET";
                myRequest.Accept = "application/vnd.hedtech.integration.v12+json";
                // myRequest.ContentLength = 0;
                myRequest.Timeout = 20000;

                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);

                strHTML = sr.ReadToEnd();
                sr.Close();
                myResponse.Close();
            }
            catch (WebException e)
            {

                using (WebResponse response = e.Response)
                {
                    HttpWebResponse httpResponse = (HttpWebResponse)response;
                    //Console.WriteLine("Error code: {0}", httpResponse.StatusCode);
                    using (Stream data = response.GetResponseStream())
                    using (var reader = new StreamReader(data))
                    {
                        // text is the response body
                        string text = reader.ReadToEnd();
                        //Console.WriteLine(text);
                        sendEmail(baseurl + "\r\n\r\n Error code:" + httpResponse.StatusCode + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + text, ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getEnrollmentData()");
                    }
                }

                //Console.WriteLine(ex.InnerException);
                strHTML = string.Empty;
            }
            /*
            catch (Exception ex)
            {
                //sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in getStudents()");
                Console.WriteLine(ex.ToString());
                strHTML = string.Empty;
            }*/
            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }


        public static string GetApiToken(string baseurl, string apikey, string sintSchoolID)
        {
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            HttpWebRequest myRequest = null;
            WebResponse myResponse = null;
            System.IO.StreamReader sr = null;
            string strHTML = string.Empty;

            try
            {

                baseurl = baseurl + "/auth";

                myRequest = (HttpWebRequest)WebRequest.Create(baseurl);
                myRequest.Headers.Add("Authorization", "Bearer " + apikey);
                //myRequest.ContentType = "application/json";
                myRequest.Method = "POST";
                // myRequest.ContentLength = 0;
                myRequest.Timeout = 20000;

                myResponse = myRequest.GetResponse();

                sr = new System.IO.StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);

                strHTML = sr.ReadToEnd();

                sr.Close();
                myResponse.Close();
            }
            catch (StackOverflowException stack)
            {
                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + stack.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in GetData()");
                //Console.WriteLine(ex.ToString());
                strHTML = string.Empty;
            }
            catch (Exception ex)
            {
                sendEmail("Url:" + baseurl + "\r\n" + "School ID: " + sintSchoolID + "\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Ellucian API Error in GetData()");
                //Console.WriteLine(ex.ToString());
                strHTML = string.Empty;
            }

            finally
            {
                myRequest = null;
                myResponse = null;
                sr = null;
            }

            return strHTML;
        }

        private static void saveStudentData(string sintSchoolID, string Id, string FullName, string EmailAddress, string StudentNumber, string StudentNumber2, string Address, string City, string State, string Zip, string PSOStudent)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertStudent"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@Id", SqlDbType.VarChar, 200).Value = Id;
                objCmd.Parameters.Add("@FullName", SqlDbType.VarChar, 200).Value = FullName;
                objCmd.Parameters.Add("@EmailAddress", SqlDbType.VarChar, 200).Value = EmailAddress;
                objCmd.Parameters.Add("@StudentNumber", SqlDbType.VarChar, 200).Value = StudentNumber;
                objCmd.Parameters.Add("@StudentNumber2", SqlDbType.VarChar, 200).Value = StudentNumber2;
                objCmd.Parameters.Add("@Address", SqlDbType.VarChar, 200).Value = Address;
                objCmd.Parameters.Add("@City", SqlDbType.VarChar, 200).Value = City;
                objCmd.Parameters.Add("@State", SqlDbType.VarChar, 200).Value = State;
                objCmd.Parameters.Add("@Zip", SqlDbType.VarChar, 200).Value = Zip;
                objCmd.Parameters.Add("@PSOStudent", SqlDbType.VarChar, 200).Value = PSOStudent;




                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveStudentData()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }

        private static void saveInstructorData(string sintSchoolID, string section_id, string Instructor_id, string fullname, string email)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertInstructorInfo"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@section_id ", SqlDbType.VarChar, 200).Value = section_id;
                objCmd.Parameters.Add("@Instructor_id", SqlDbType.VarChar, 200).Value = Instructor_id;
                objCmd.Parameters.Add("@fullname", SqlDbType.VarChar, 200).Value = fullname;
                objCmd.Parameters.Add("@email", SqlDbType.VarChar, 200).Value = email;




                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveInstructorData()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }
        private static void saveDrakeIABook(string sintSchoolID, string studentID, string aidYear, string awardFund)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertIABooks"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@StudentId ", SqlDbType.VarChar, 200).Value = studentID;
                objCmd.Parameters.Add("@aidYear", SqlDbType.VarChar, 200).Value = aidYear;
                objCmd.Parameters.Add("@awardFund", SqlDbType.VarChar, 200).Value = awardFund;



                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveDrakeIABook()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }

        private static void saveSitesData(string sintSchoolID, string id, string title, string code)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertSites"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@id ", SqlDbType.VarChar, 200).Value = id;
                objCmd.Parameters.Add("@title", SqlDbType.VarChar, 200).Value = title;
                objCmd.Parameters.Add("@code", SqlDbType.VarChar, 200).Value = code;



                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveSitesData()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }

        private static void saveTermData(string sintSchoolID, string id, string title, string startOn, string endOn, string code, string parent_id, string category_type)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertTerm"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@id ", SqlDbType.VarChar, 200).Value = id;
                objCmd.Parameters.Add("@title", SqlDbType.VarChar, 200).Value = title;
                objCmd.Parameters.Add("@startOn", SqlDbType.VarChar, 200).Value = startOn;
                objCmd.Parameters.Add("@endOn", SqlDbType.VarChar, 200).Value = endOn;
                objCmd.Parameters.Add("@code", SqlDbType.VarChar, 200).Value = code;
                objCmd.Parameters.Add("@parent_id", SqlDbType.VarChar, 200).Value = parent_id;
                objCmd.Parameters.Add("@category_type", SqlDbType.VarChar, 200).Value = category_type;



                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveTermData()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }
        private static void saveEnrollmentData(string sintSchoolID, string sectionID, string studentID, string registrationStatus, string sectionRegistrationStatusReason)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertEnrollment"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@sectionID ", SqlDbType.VarChar, 200).Value = sectionID;
                objCmd.Parameters.Add("@studentID", SqlDbType.VarChar, 200).Value = studentID;
                objCmd.Parameters.Add("@registrationStatus", SqlDbType.VarChar, 200).Value = registrationStatus;
                objCmd.Parameters.Add("@sectionRegistrationStatusReason", SqlDbType.VarChar, 200).Value = sectionRegistrationStatusReason;


                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveEnrollmentData()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }


        private static void saveCourseData(string sintSchoolID, string courseID, string subjectID, string title, string title2, string courseNumber, string subject, string subject_short, string creditHours)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertCourseInfo"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@courseID ", SqlDbType.VarChar, 200).Value = courseID;
                objCmd.Parameters.Add("@subjectID ", SqlDbType.VarChar, 200).Value = subjectID;
                objCmd.Parameters.Add("@title ", SqlDbType.VarChar, 200).Value = title;
                objCmd.Parameters.Add("@title2 ", SqlDbType.VarChar, 200).Value = title2;
                objCmd.Parameters.Add("@courseNumber ", SqlDbType.VarChar, 200).Value = courseNumber;
                objCmd.Parameters.Add("@subject ", SqlDbType.VarChar, 200).Value = subject;
                objCmd.Parameters.Add("@subject_short ", SqlDbType.VarChar, 200).Value = subject_short;
                objCmd.Parameters.Add("@creditHours ", SqlDbType.VarChar, 200).Value = creditHours;


                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveCourseData()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }

        private static void saveSectionData(string sintSchoolID, string sectionID, string courseID, string title, string startOn, string endOn, string code, string vcLevel1, string vcLevel2, string vcLevel3, string number, string maxEnrollment, string academicPeriod, string creditHours, string status, string site_id, string zeroTextbookCost)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"];
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["insertSection"];
                objCmd.CommandType = CommandType.StoredProcedure;
                objCmd.CommandTimeout = 100;

                objCmd.Parameters.Add("@sintSchoolID", SqlDbType.SmallInt).Value = sintSchoolID;
                objCmd.Parameters.Add("@courseID ", SqlDbType.VarChar, 200).Value = courseID;
                objCmd.Parameters.Add("@sectionID", SqlDbType.VarChar, 200).Value = sectionID;
                objCmd.Parameters.Add("@title", SqlDbType.VarChar, 200).Value = title;
                objCmd.Parameters.Add("@startOn", SqlDbType.VarChar, 200).Value = startOn;
                objCmd.Parameters.Add("@endOn", SqlDbType.VarChar, 200).Value = endOn;
                objCmd.Parameters.Add("@code", SqlDbType.VarChar, 200).Value = code;
                objCmd.Parameters.Add("@vcLevel1", SqlDbType.VarChar, 200).Value = vcLevel1;
                objCmd.Parameters.Add("@vcLevel2", SqlDbType.VarChar, 200).Value = vcLevel2;
                objCmd.Parameters.Add("@vcLevel3", SqlDbType.VarChar, 200).Value = vcLevel3;
                objCmd.Parameters.Add("@number", SqlDbType.VarChar, 200).Value = number;
                objCmd.Parameters.Add("@maxEnrollment", SqlDbType.VarChar, 200).Value = maxEnrollment;
                objCmd.Parameters.Add("@academicPeriod", SqlDbType.VarChar, 200).Value = academicPeriod;
                objCmd.Parameters.Add("@creditHours", SqlDbType.VarChar, 200).Value = creditHours;
                objCmd.Parameters.Add("@status", SqlDbType.VarChar, 200).Value = status;
                objCmd.Parameters.Add("@site_id", SqlDbType.VarChar, 200).Value = site_id;
                objCmd.Parameters.Add("@zeroTextbookCost", SqlDbType.VarChar, 200).Value = zeroTextbookCost;


                objCmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                sendEmail("Error in Ellucian APi()\r\n\r\n" + ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in saveSectionData()");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objConn.Dispose();

                objCmd = null;
                objConn = null;
            }

        }

        private static DataTable getSchoolIDs()
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            SqlDataAdapter objDA = new SqlDataAdapter();
            DataTable objDT = new DataTable();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["FASTConnectionString"].ToString();
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = ConfigurationManager.AppSettings["GetSchoolsSP"];
                objCmd.CommandType = CommandType.StoredProcedure;
                if (ConfigurationManager.AppSettings["ForceSchoolID"].ToString() != String.Empty)
                    objCmd.Parameters.Add("@sintSchoolID", SqlDbType.Int).Value = ConfigurationManager.AppSettings["ForceSchoolID"];

                objCmd.CommandTimeout = 600;

                objDA.SelectCommand = objCmd;
                objDA.Fill(objDT);


            }
            catch (Exception ex)
            {
                sendEmail(ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in EllucianAPI_Banner [getSchoolIDs()]");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objDA.Dispose();
                objConn.Dispose();

                objCmd = null;
                objDA = null;
                objConn = null;
            }

            return objDT;
        }
        private static DataTable getStudentIDs(string sintSchoolID)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            SqlDataAdapter objDA = new SqlDataAdapter();
            DataTable objDT = new DataTable();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"].ToString();
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = "select distinct studentID from tblEllucianEnrollment_Banner where registrationStatus = 'registered' and chrProcessingStatus = 'X' and sintSchoolID = " + sintSchoolID;
                objCmd.CommandType = CommandType.Text;


                objCmd.CommandTimeout = 600;

                objDA.SelectCommand = objCmd;
                objDA.Fill(objDT);


            }
            catch (Exception ex)
            {
                sendEmail(ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in EllucianAPI_Banner [getTermIDs()]");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objDA.Dispose();
                objConn.Dispose();

                objCmd = null;
                objDA = null;
                objConn = null;
            }

            return objDT;
        }

        private static DataTable getTermIDs(string sintSchoolID)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            SqlDataAdapter objDA = new SqlDataAdapter();
            DataTable objDT = new DataTable();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"].ToString();
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = "select distinct academicPeriod from tblEllucianSections_Banner where chrProcessingStatus = 'X' and sintSchoolID = " + sintSchoolID;
                objCmd.CommandType = CommandType.Text;


                objCmd.CommandTimeout = 600;

                objDA.SelectCommand = objCmd;
                objDA.Fill(objDT);


            }
            catch (Exception ex)
            {
                sendEmail(ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in EllucianAPI_Banner [getTermIDs()]");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objDA.Dispose();
                objConn.Dispose();

                objCmd = null;
                objDA = null;
                objConn = null;
            }

            return objDT;
        }
        private static DataTable getSectionIDForInstructors(string sintSchoolID)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            SqlDataAdapter objDA = new SqlDataAdapter();
            DataTable objDT = new DataTable();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"].ToString();
                objConn.Open();

                objCmd.Connection = objConn;
                objCmd.CommandText = "select distinct sectionID from tblEllucianSections_Banner where chrProcessingStatus = 'X' and sintSchoolID =  " + sintSchoolID;
                //objCmd.CommandText = "select distinct id from tblEllucianTerms where chrProcessingStatus = 'X' and sintSchoolID =  " + sintSchoolID;
                objCmd.CommandType = CommandType.Text;


                objCmd.CommandTimeout = 600;

                objDA.SelectCommand = objCmd;
                objDA.Fill(objDT);


            }
            catch (Exception ex)
            {
                sendEmail(ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in EllucianAPI_Banner [getSectionIDForInstructors()]");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objDA.Dispose();
                objConn.Dispose();

                objCmd = null;
                objDA = null;
                objConn = null;
            }

            return objDT;
        }

        private static DataTable getCourseIDsFromSections(string sintSchoolID)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            SqlDataAdapter objDA = new SqlDataAdapter();
            DataTable objDT = new DataTable();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"].ToString();
                objConn.Open();

                objCmd.Connection = objConn;
                //objCmd.CommandText = "select distinct sectionID from tblEllucianSections where chrProcessingStatus = 'X' and sintSchoolID =  " + sintSchoolID;
                objCmd.CommandText = "select distinct courseID from tblEllucianSections_Banner where chrProcessingStatus = 'X' and sintSchoolID =  " + sintSchoolID;
                objCmd.CommandType = CommandType.Text;


                objCmd.CommandTimeout = 600;

                objDA.SelectCommand = objCmd;
                objDA.Fill(objDT);


            }
            catch (Exception ex)
            {
                sendEmail(ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in EllucianAPI_Banner [getCourseIDsFromSections()]");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objDA.Dispose();
                objConn.Dispose();

                objCmd = null;
                objDA = null;
                objConn = null;
            }

            return objDT;
        }


        private static DataTable getTermIDForEnrollment(string sintSchoolID)
        {

            SqlConnection objConn = new SqlConnection();
            SqlCommand objCmd = new SqlCommand();

            SqlDataAdapter objDA = new SqlDataAdapter();
            DataTable objDT = new DataTable();

            try
            {
                objConn.ConnectionString = ConfigurationManager.AppSettings["StagingConnectionString"].ToString();
                objConn.Open();

                objCmd.Connection = objConn;
                //objCmd.CommandText = "select distinct sectionID from tblEllucianSections where chrProcessingStatus = 'X' and sintSchoolID =  " + sintSchoolID;
                objCmd.CommandText = "select distinct id from tblEllucianTerms_Banner where chrProcessingStatus = 'X' and sintSchoolID =  " + sintSchoolID;
                objCmd.CommandType = CommandType.Text;


                objCmd.CommandTimeout = 600;

                objDA.SelectCommand = objCmd;
                objDA.Fill(objDT);


            }
            catch (Exception ex)
            {
                sendEmail(ex.ToString(), ConfigurationManager.AppSettings["EmailFrom"].ToString(), ConfigurationManager.AppSettings["EmailTo"].ToString(), "Error in EllucianAPI_Banner [getSectionIDs()]");
            }
            finally
            {
                if (objConn != null)
                    objConn.Close();

                objCmd.Dispose();
                objDA.Dispose();
                objConn.Dispose();

                objCmd = null;
                objDA = null;
                objConn = null;
            }

            return objDT;
        }



        private static void sendEmail(string body, string from, string recipient_list, string subject)
        {
            clsEmail objEmail = new clsEmail();

            objEmail.Body = body;
            objEmail.From = from;
            objEmail.RecipientList = recipient_list;
            objEmail.Subject = subject;
            objEmail.SendEmail();

            objEmail = null;
        }
    }
}
