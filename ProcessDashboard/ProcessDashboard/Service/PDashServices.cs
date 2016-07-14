﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Fusillade;
using ProcessDashboard.APIRoot;
using ProcessDashboard.DBWrapper;
using ProcessDashboard.DTO;
using ProcessDashboard.Model;
using ProcessDashboard.Service.Interface;

//using Plugin.Connectivity;
//using Polly;
namespace ProcessDashboard.Service_Access_Layer
{
    /*
     * 
     * Name: PDashServices.cs
     * 
     * Purpose: This class is a concerete implementation for IPDashServices interface.
     * 
     * Description:
     * This class provides concrete implemntation for getting values either from remote service or from local database.
     * The remote service will inturn make use of a concrete implementation of IApiTypes interface.
     * The local service use of the Database Manager for connecting to SQlite Database.

     */
    
    public class PDashServices : IPDashServices
    {
        // Api Service for making the request using Fusilade
        private readonly IApiTypes _apiService;
        // DB Manager to manage Database operations
        private readonly DBManager _dbm;

        public PDashServices(IApiTypes apiService)
        {
            _apiService = apiService;
            _dbm = DBManager.getInstance();
        }

        /*
         * List of projects 
         * 
         */ 

        public async Task<List<Project>> GetProjectsListLocal(Priority priority, string dataset)
        {
            System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + " Going to get data from DB");

            List<Project> output = null;

            List<ProjectModel> values = _dbm.pw.GetAllRecords();

            if (values == null || values.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + "Nothing in the DB");
                return null;
            } else {
                // Map from project model to project and return values.
                output = Mapper.GetInstance().toProjectList(values);
            }

            System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + " Done with that");

            return output;
        }

        public async Task<List<Project>> GetProjectsListRemote(Priority priority, string dataset)
        {
            System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + " Going for remote task");

            Task<ProjectsListRoot> getTaskDtoTask;

            System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + " Setting priority");
            switch (priority)
            {
                case Priority.Background:
                    getTaskDtoTask = _apiService.Background.GetProjectsList(dataset);
                    break;
                case Priority.UserInitiated:
                    getTaskDtoTask = _apiService.UserInitiated.GetProjectsList(dataset);
                    break;
                case Priority.Speculative:
                    getTaskDtoTask = _apiService.Speculative.GetProjectsList(dataset);
                    break;
                default:
                    getTaskDtoTask = _apiService.UserInitiated.GetProjectsList(dataset);
                    break;
            }

            ProjectsListRoot projects = await getTaskDtoTask;

            //var gitHubApi = RestService.For<IPDashApi>("https://pdes.tuma-solutions.com/api/v1/");
            //ProjectsListRoot projects = await gitHubApi.GetProjectsList("mock");

            System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + "Got the content I guess");
            System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + projects.stat);
            System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + (projects.projects.Count));

            if (!projects.stat.Equals("ok") || projects.projects==null)
            {
                System.Diagnostics.Debug.WriteLine("ProjectModel Service : " + "Null here");
                return null;
            }

            // Convert to model and store in DB
            //List<ProjectModel> output = Mapper.GetInstance().toProjectModelList(projects.projects);
            //_dbm.pw.insertMultipleRecords(output);

            //test(projects);

            /*
            if (CrossConnectivity.Current.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Setting connection policy");
                task = await Policy
                    .Handle<Exception>()
                    .RetryAsync(retryCount: 5)
                    .ExecuteAsync(async () => await getTaskDtoTask);
            }
            */
            return projects.projects;
        }

        /*
         * Task APIs
         */

        public async Task<List<DTO.Task>> GetTasksListLocal(Priority priority, string dataset, string projectID)
        {
            System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Going to get data from DB");
            List<DTO.Task> output = null;

            List<TaskModel> values = _dbm.tw.GetAllRecords();

            if (values == null || values.Count == 0)
            {
                return null;
            }
            else
            {
                // Map from project model to project and return values.
                output = Mapper.GetInstance().toTaskList(values);
            }

            System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Done with that");

            return output;
        }

        public async Task<List<DTO.Task>> GetTasksListRemote(Priority priority, string dataset, string projectID)
        {
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Going for remote task");

            TaskListRoot tasks = null;
            Task<TaskListRoot> getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Setting priority");
            switch (priority)
            {
                case Priority.Background:
                    getTaskDtoTask = _apiService.Background.GetTasksList(dataset,projectID);
                    break;
                case Priority.UserInitiated:
                    getTaskDtoTask = _apiService.UserInitiated.GetTasksList(dataset, projectID);
                    break;
                case Priority.Speculative:
                    getTaskDtoTask = _apiService.Speculative.GetTasksList(dataset, projectID);
                    break;
                default:
                    getTaskDtoTask = _apiService.UserInitiated.GetTasksList(dataset, projectID);
                    break;
            }

            tasks = await getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + "Got the content. STATUS :"+tasks.stat);
            System.Diagnostics.Debug.WriteLine("Task Service : " + "Is null : " + (tasks.projectTasks==null));

            Project p = tasks.forProject;
            for (int i = 0; i < tasks.projectTasks.Count; i++)
            {
                DTO.Task t = tasks.projectTasks[i];
                t.project = p;
                tasks.projectTasks[i] = t;
            }

            /*
            if (CrossConnectivity.Current.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Setting connection policy");
                task = await Policy
                    .Handle<Exception>()
                    .RetryAsync(retryCount: 5)
                    .ExecuteAsync(async () => await getTaskDtoTask);
            }
            */
            return tasks.projectTasks;
        }
        
        public async Task<DTO.Task> GetTaskDetailsLocal(Priority priority, string dataset, string projecttaskID)
        {
            System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Going to get data from DB");
            DTO.Task output = null;

            TaskModel values = _dbm.tw.getRecord(projecttaskID);

            if (values == null)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Nothing in DB");
                // DB does not have any values. Get the values from the server.
                output = await GetTaskDetailsRemote(priority, dataset, projecttaskID);

                // Map from task to task model
                values = Mapper.GetInstance().toTaskModel(output);
                // Store in DB
                _dbm.tw.insertRecord(values);

            }
            else
            {
                // Map from project model to project and return values.
                output = Mapper.GetInstance().toTask(values);
            }

            System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Done with that");

            return output;
        }

        public async Task<DTO.Task> GetTaskDetailsRemote(Priority priority, string dataset, string projecttaskID)
        {
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Going for remote task");

            TaskRoot task = null;
            Task<TaskRoot> getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Setting priority");
            switch (priority)
            {
                case Priority.Background:
                    getTaskDtoTask = _apiService.Background.GetTaskDetails(dataset, projecttaskID);
                    break;
                case Priority.UserInitiated:
                    getTaskDtoTask = _apiService.UserInitiated.GetTaskDetails(dataset, projecttaskID);
                    break;
                case Priority.Speculative:
                    getTaskDtoTask = _apiService.Speculative.GetTaskDetails(dataset, projecttaskID);
                    break;
                default:
                    getTaskDtoTask = _apiService.UserInitiated.GetTaskDetails(dataset, projecttaskID);
                    break;
            }

            task = await getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + "Got the content I guess");

            // Convert to model and store in DB

            TaskModel output = Mapper.GetInstance().toTaskModel(task.task);
            _dbm.tw.insertRecord(output);

            /*
            if (CrossConnectivity.Current.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Setting connection policy");
                task = await Policy
                    .Handle<Exception>()
                    .RetryAsync(retryCount: 5)
                    .ExecuteAsync(async () => await getTaskDtoTask);
            }
            */
            return task.task;
        }

        public async Task<List<DTO.Task>> GetRecentTasksLocal(Priority priority, string dataset)
        {
            System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Going to get data from DB");
            List<DTO.Task> output = null;

            List<TaskModel> values = _dbm.tw.GetAllRecords();

            values.Sort((val1, val2) => val1.RecentOrdinal.CompareTo(val2.RecentOrdinal));

            if (values == null || values.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Nothing in DB");
                // DB does not have any values. Get the values from the server.
                output = await GetRecentTasksRemote(priority, dataset);

                // Map from project to project model
                values = Mapper.GetInstance().toTaskModelList(output);
                
                // Update Recent ordinal
                
                //dbm.tw.insertMultipleRecords(values);

            }
            else
            {
                // Map from project model to project and return values.
                output = Mapper.GetInstance().toTaskList(values);
            }

            System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Done with that");

            return output;



        }

        public async Task<List<DTO.Task>> GetRecentTasksRemote(Priority priority, string dataset)
        {
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Going for remote task");

            RecentTasksRoot task = null;
            Task<RecentTasksRoot> getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Setting priority");
            switch (priority)
            {
                case Priority.Background:
                    getTaskDtoTask = _apiService.Background.GetRecentTasks(dataset);
                    break;
                case Priority.UserInitiated:
                    getTaskDtoTask = _apiService.UserInitiated.GetRecentTasks(dataset);
                    break;
                case Priority.Speculative:
                    getTaskDtoTask = _apiService.Speculative.GetRecentTasks(dataset);
                    break;
                default:
                    getTaskDtoTask = _apiService.UserInitiated.GetRecentTasks(dataset);
                    break;
            }

            task = await getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + "Got the content I guess");
            
            // Convert to model and store in DB

            List<TaskModel> output = Mapper.GetInstance().toTaskModelList(task.recentTasks);

            // TODO: UPdate Recent Ordinal
            /*
            if (CrossConnectivity.Current.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Setting connection policy");
                task = await Policy
                    .Handle<Exception>()
                    .RetryAsync(retryCount: 5)
                    .ExecuteAsync(async () => await getTaskDtoTask);
            }
            */
            return task.recentTasks;
        }

        public async Task<List<TimeLogEntry>> GetTimeLogsLocal(Priority priority, string dataset)
        {
            System.Diagnostics.Debug.WriteLine("Time log Service : " + " Going to get data from DB");
            List<DTO.TimeLogEntry> output = null;

            List<TimeLogEntryModel> values = _dbm.tlw.GetAllRecords();

            if (values == null || values.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Nothing in DB");
                // DB does not have any values. Get the values from the server.
            //    output = await GetTimeLogsRemote(priority, dataset);

                // Map from project to project model
                values = Mapper.GetInstance().toTimeLogEntryModelList(output);
                // Store in DB
                _dbm.tlw.insertMultipleRecords(values);

            }
            else
            {
                // Map from project model to project and return values.
                output = Mapper.GetInstance().toTimeLogEntryList(values);
            }

            System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Done with that");

            return output;


        }

        public async Task<List<TimeLogEntry>> GetTimeLogsRemote(Priority priority, string dataset, int maxResults, string startDateFrom, string startDateTo, string taskId, string projectId)
        {
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Going for remote task");

            TimeLogsRoot task = null;
            Task<TimeLogsRoot> getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + " Setting priority");
            switch (priority)
            {
                case Priority.Background:
                    getTaskDtoTask = _apiService.Background.GetTimeLogs(dataset,maxResults,  startDateFrom,  startDateTo,  taskId,  projectId);
                    break;
                case Priority.UserInitiated:
                    getTaskDtoTask = _apiService.UserInitiated.GetTimeLogs(dataset, maxResults, startDateFrom, startDateTo, taskId, projectId);
                    break;
                case Priority.Speculative:
                    getTaskDtoTask = _apiService.Speculative.GetTimeLogs(dataset, maxResults, startDateFrom, startDateTo, taskId, projectId);
                    break;
                default:
                    getTaskDtoTask = _apiService.UserInitiated.GetTimeLogs(dataset, maxResults, startDateFrom, startDateTo, taskId, projectId);
                    break;
            }

            task = await getTaskDtoTask;
            System.Diagnostics.Debug.WriteLine("Task Service : " + "Got the content I guess");

            // Convert to model and store in DB

            //List<TimeLogEntryModel> output = Mapper.GetInstance().toTimeLogEntryModelList(task.timeLogEntries);
            //_dbm.tlw.insertMultipleRecords(output);

            /*
            if (CrossConnectivity.Current.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("TaskModel Service : " + " Setting connection policy");
                task = await Policy
                    .Handle<Exception>()
                    .RetryAsync(retryCount: 5)
                    .ExecuteAsync(async () => await getTaskDtoTask);
            }
            */
            return task.timeLogEntries;
        }


    }




}