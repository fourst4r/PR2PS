﻿using PR2PS.Common.Constants;
using PR2PS.Common.Exceptions;
using PR2PS.Common.Extensions;
using PR2PS.DataAccess.Core;
using PR2PS.DataAccess.Entities;
using PR2PS.Web.Core;
using PR2PS.Web.Core.FormModels;
using PR2PS.Web.Core.JsonModels;
using PR2PS.Web.Core.Management;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;

namespace PR2PS.Web.Controllers
{
    public class MessagingController : ApiController
    {
        private IDataAccessEngine dataAccess;

        public MessagingController(IDataAccessEngine dataAccess)
        {
            this.dataAccess = dataAccess;
        }

        /// <summary>
        /// Gets private messages.
        /// </summary>
        /// <returns>List of private messages or error.</returns>
        [HttpPost]
        [Route("messages_get.php")]
        public HttpResponseMessage GetMessages([FromBody] GetMessagesFormModel getMessagesData)
        {
            try
            {
                if (getMessagesData == null)
                {
                    return HttpResponseFactory.Response200Json(new ErrorJson { Error = ErrorMessages.ERR_NO_FORM_DATA } );
                }

                SessionInstance mySession = SessionManager.Instance.GetSessionByToken(getMessagesData.Token);
                if (mySession == null)
                {
                    return HttpResponseFactory.Response200Json(new ErrorJson { Error = ErrorMessages.ERR_NOT_LOGGED_IN } );
                }

                IEnumerable<Message> messages = this.dataAccess.GetMessages(mySession.AccounData.UserId, getMessagesData.Start, getMessagesData.Count);

                return HttpResponseFactory.Response200Json(new MessageListJson()
                {
                    Messages = messages.Select(m => new MessageJson
                    {
                        MessageId = m.Id,
                        Message = m.Content,
                        Time = m.DateSent,
                        UserId = m.Sender.Id,
                        Name = m.Sender.Username,
                        Group = m.Sender.Group
                    }).ToList(),
                    Success = true
                });
            }
            catch (PR2Exception ex)
            {
                return HttpResponseFactory.Response200Json(new ErrorJson { Error = ex.Message });
            }
            catch (Exception ex)
            {
                return HttpResponseFactory.Response500Plain(ex.Message);
            }
        }

        /// <summary>
        /// Sends private message to specified recipient.
        /// </summary>
        /// <returns>Status indicating whether action was successful.</returns>
        [HttpPost]
        [Route("message_send.php")]
        public HttpResponseMessage SendMessage([FromBody] SendMessageFormModel sendMessageData)
        {
            try
            {
                if (sendMessageData == null)
                {
                    return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NO_FORM_DATA);
                }

                if (String.IsNullOrEmpty(sendMessageData.To_Name)) sendMessageData.To_Name = String.Empty;

                SessionInstance mySession = SessionManager.Instance.GetSessionByToken(sendMessageData.Token);
                if (mySession == null)
                {
                    return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NOT_LOGGED_IN);
                }

                using (DatabaseContext db = new DatabaseContext("PR2Context"))
                {
                    Account sender = db.Accounts.FirstOrDefault(a => a.Id == mySession.AccounData.UserId);
                    Account recipient = db.Accounts.FirstOrDefault(a => a.Username.ToUpper() == sendMessageData.To_Name.ToUpper());
                    
                    if (recipient == null)
                    {
                        return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NO_USER_WITH_SUCH_NAME);
                    }

                    recipient.Messages.Add(new Message
                    {
                        Sender = sender,
                        Recipient = recipient,
                        Content = sendMessageData.Message ?? String.Empty,
                        DateSent = DateTime.UtcNow.GetSecondsSinceUnixTime(),
                        IPAddress = this.Request.GetRemoteIPAddress()
                    });
                    db.SaveChanges();

                    return HttpResponseFactory.Response200Plain(StatusKeys.MESSAGE, StatusMessages.MESSAGE_SENT);
                }
            }
            catch (Exception ex)
            {
                return HttpResponseFactory.Response200Plain(ex.Message);
            }
        }

        /// <summary>
        /// Reports the message for the administration.
        /// TODO - Implement.
        /// </summary>
        /// <returns>Status indicating whether action was successful.</returns>
        [HttpPost]
        [Route("message_report.php")]
        public HttpResponseMessage ReportMessage([FromBody] DeleteOrReportMessageFormModel reportMessageData)
        {
            try
            {
                if (reportMessageData == null
                    || !reportMessageData.Message_Id.HasValue)
                {
                    return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NO_FORM_DATA);
                }

                SessionInstance mySession = SessionManager.Instance.GetSessionByToken(reportMessageData.Token);
                if (mySession == null)
                {
                    return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NOT_LOGGED_IN);
                }

                return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NOT_IMPLEMENTED);
            }
            catch (Exception ex)
            {
                return HttpResponseFactory.Response500Plain(ex.Message);
            }
        }

        /// <summary>
        /// Marks the message specified by message id as deleted.
        /// </summary>
        /// <returns>Status indicating whether action was successful.</returns>
        [HttpPost]
        [Route("message_delete.php")]
        public HttpResponseMessage DeleteMessage([FromBody] DeleteOrReportMessageFormModel reportMessageData)
        {
            try
            {
                if (reportMessageData == null
                    || !reportMessageData.Message_Id.HasValue)
                {
                    return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NO_FORM_DATA);
                }

                SessionInstance mySession = SessionManager.Instance.GetSessionByToken(reportMessageData.Token);
                if (mySession == null)
                {
                    return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NOT_LOGGED_IN);
                }

                using (DatabaseContext db = new DatabaseContext("PR2Context"))
                {
                    Account accModel = db.Accounts.FirstOrDefault(a => a.Id == mySession.AccounData.UserId);
                    Message message = accModel.Messages.FirstOrDefault(m => m.Id == reportMessageData.Message_Id.Value);
                    if (message == null)
                    {
                        return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_MESSAGE_NOT_FOUND);
                    }

                    message.IsDeleted = true;
                    db.SaveChanges();

                    return HttpResponseFactory.Response200Plain(StatusKeys.SUCCESS, StatusMessages.TRUE);
                }
            }
            catch (Exception ex)
            {
                return HttpResponseFactory.Response500Plain(ex.Message);
            }
        }

        /// <summary>
        /// Deletes all messages from the inbox.
        /// </summary>
        /// <param name="token">Session token.</param>
        /// <param name="rand">Random string.</param>
        /// <returns>Status indicating whether action was successful.</returns>
        [HttpGet]
        [Route("messages_delete_all.php")]
        public HttpResponseMessage DeleteAllMessages(String token = "", String rand = "")
        {
            try
            {
                SessionInstance mySession = SessionManager.Instance.GetSessionByToken(token);
                if (mySession == null)
                {
                    return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NOT_LOGGED_IN);
                }

                using (DatabaseContext db = new DatabaseContext("PR2Context"))
                {
                    Account accModel = db.Accounts.FirstOrDefault(a => a.Id == mySession.AccounData.UserId);
                    IEnumerable<Message> messages = accModel.Messages.Where(m => !m.IsDeleted);
                    
                    if (messages == null || !messages.Any())
                    {
                        return HttpResponseFactory.Response200Plain(StatusKeys.ERROR, ErrorMessages.ERR_NO_MESSAGES);
                    }

                    foreach (Message message in messages)
                    {
                        message.IsDeleted = true;
                    }
                    db.SaveChanges();

                    return HttpResponseFactory.Response200Plain(StatusKeys.SUCCESS, StatusMessages.TRUE);
                }
            }
            catch (Exception ex)
            {
                return HttpResponseFactory.Response500Plain(ex.Message);
            }
        }
    }
}
