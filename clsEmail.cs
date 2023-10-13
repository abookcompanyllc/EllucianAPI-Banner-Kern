using System;
using System.IO;
using System.Collections;

/*
<doc author="Kevin Scott" date="07/08/2005">
	This class will send emails with or without attachments.
	(Converted from VB.NET around 10/31/2005)
</doc> 
*/

namespace utility
{
    /// <summary>
    /// Summary description for clsEmail.
    /// </summary>
    public class clsEmail
    {
        #region Declarations
        private string strRecipientList;
        private string strSender;
        private string strBody;
        private string strFrom;
        private string strSubject;
        private string strCC;
        private string strBCC;
        private string strError;
        private string strServer;
        private int intPort;
        private ArrayList strAttachment;
        #endregion

        #region Constructor
        public clsEmail()
        {
            strRecipientList = string.Empty;
            strSender = string.Empty;
            strBody = string.Empty;
            strFrom = string.Empty;
            strSubject = string.Empty;
            strCC = string.Empty;
            strBCC = string.Empty;
            strAttachment = new ArrayList();
            strError = string.Empty;
            strServer = string.Empty;
            intPort = 0;
        }

        #endregion

        #region Functions
        public bool SendEmail()
        {
            OpenSmtp.Mail.EmailAddress From = new OpenSmtp.Mail.EmailAddress(strFrom, strSender);
            OpenSmtp.Mail.MailMessage msg = new OpenSmtp.Mail.MailMessage();
            OpenSmtp.Mail.Smtp Server = new OpenSmtp.Mail.Smtp("mail.ecampus.com", 25);
            ArrayList strEmailList = new ArrayList();
            OpenSmtp.Mail.Attachment objAttachment;
            OpenSmtp.Mail.EmailAddress objCC;
            bool bSendEmail = false;

            strError = string.Empty;

            try
            {
                if (strRecipientList != string.Empty)
                {
                    msg.From = From;
                    msg.Subject = strSubject;
                    msg.Body = strBody;

                    foreach (string strEmail in strRecipientList.Split(';'))
                        msg.AddRecipient(strEmail, OpenSmtp.Mail.AddressType.To);


                    if (strCC != string.Empty)
                    {
                        // Add CC.
                        foreach (string strEmail in strCC.Split(';'))
                        {
                            objCC = new OpenSmtp.Mail.EmailAddress(strEmail);
                            strEmailList.Add(objCC);
                        }

                        msg.CC = strEmailList;
                    }

                    strEmailList = new ArrayList();

                    if (strBCC != string.Empty)
                    {
                        // Add BCC.
                        foreach (string strEmail in strBCC.Split(';'))
                        {
                            objCC = new OpenSmtp.Mail.EmailAddress(strEmail);
                            strEmailList.Add(objCC);
                        }

                        msg.BCC = strEmailList;
                    }

                    if (strAttachment.Count > 0)
                    {
                        foreach (string strAttachmentPath in strAttachment)
                        {
                            objAttachment = new OpenSmtp.Mail.Attachment(strAttachmentPath);
                            msg.AddAttachment(objAttachment);
                        }
                    }

                    Server.SendMail(msg);

                    bSendEmail = true;
                }
            }
            catch (Exception ex)
            {
                strError = ex.ToString();
                bSendEmail = false;
            }
            finally
            {
                objAttachment = null;
                objCC = null;
                Server = null;
                msg = null;
                From = null;
            }

            return bSendEmail;
        }
        #endregion

        #region Properties
        public string Server
        {
            get { return strServer; }
            set { strServer = value; }
        }

        public int Port
        {
            get { return intPort; }
            set { intPort = value; }
        }

        public string RecipientList
        {
            get { return strRecipientList; }
            set { strRecipientList = value; }
        }

        public string Sender
        {
            get { return strSender; }
            set { strSender = value; }
        }

        public string From
        {
            get { return strFrom; }
            set { strFrom = value; }
        }

        public string Body
        {
            get { return strBody; }
            set { strBody = value; }
        }

        public string Subject
        {
            get { return strSubject; }
            set { strSubject = value; }
        }

        public string CC
        {
            get { return strCC; }
            set { strCC = value; }
        }

        public string BCC
        {
            get { return strBCC; }
            set { strBCC = value; }
        }

        public ArrayList Attachment
        {
            get { return strAttachment; }
            set { strAttachment = value; }
        }

        public string ErrorMessage
        {
            get { return strError; }
        }
        #endregion
    }
}
