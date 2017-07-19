(function() {
    //'use strict';

    angular
        .module('app')
        .factory('localizationService', localizationService);

    localizationService.$inject = ['$http', '$q', '$timeout'];

    function localizationService($http, $q, $timeout) {
        var service = {
            error: null,
            baseUrl: "./",
            getResourceList: getResourceList,
            resourceList: [],
            getResourceItems: getResourceItems,
            resourceItems: [],
            getResourceGridItems: getResourceGridItems,
            resourceId: null,
            getResourceSets: getResourceSets,
            resourceSets: [],
            getAllLocaleIds: getAllLocaleIds,
            localeIds: [],
            resourceStrings: [],
            localizationInfo: null,
            getResourceStrings: getResourceStrings,
            updateResourceString: updateResourceString,
            updateResource: updateResource,
            updateComment: updateComment,
            deleteResource: deleteResource,
            renameResource: renameResource,
            deleteResourceSet: deleteResourceSet,
            renameResourceSet: renameResourceSet,
            reloadResources: reloadResources,
            isRtl: isRtl,
            backup: backup,
            createTable: createTable,
            createClass: createClass,
            exportResxResources: exportResxResources,
            importResxResources: importResxResources,
            getLocalizationInfo: getLocalizationInfo,
            isLocalizationTable: isLocalizationTable
        };
        return service;

        function getResourceList(resourceSet) {            
            return $http.get("api/GetResourceListHtml?ResourceSet=" + resourceSet)
                .success(function(resourceList) {
                    service.resourceList = resourceList;
                })
                .error(parseHttpError);
        }

        function getResourceSets() {
            return $http.get("api/GetResourceSets")
                .success(function(resourceSets) {
                    service.resourceSets = resourceSets;
                })
                .error(parseHttpError);
        }

        function isLocalizationTable() {
            return $http.get("api/IsLocalizationTable");
        }

        function getAllLocaleIds(resourceSet) {
            return $http.get("api/GetAllLocaleIds?ResourceSet=" + resourceSet)
                .success(function(localeIds) {
                    service.localeIds = localeIds;
                })
                .error(parseHttpError);
        }

        function getResourceItems(resourceId, resourceSet) {
            return $http.get("api/GetResourceItems?" + $.param({resourceId, resourceSet}))
                .success(function (resourceItems) {
                    service.resourceItems = resourceItems;
                })
                .error(parseHttpError);
        }

        function getResourceGridItems(resourceSet) {
            return $http.get("api/GetAllResourcesForResourceGrid?resourceSet=" + resourceSet)
                .error(parseHttpError);
        }


        function getResourceItem(resourceId, resourceSet, lang) {
            return $http.post("api/GetResourceItem?resourceId=" + resourceId + "&resourceSet=" + resourceSet + "&cultureName=" + lang)
                .success(function(resource) {
                    service.resourceItem = resource;
                })
                .error(parseHttpError);
        }

        function getResourceStrings(resourceId, resourceSet) {
            return $http.get("api/GetResourceStrings?ResourceId=" + resourceId + "&ResourceSet=" + resourceSet)
                .success(function (resourceStrings) {
                    service.resourceStrings = resourceStrings;
                })
                .error(parseHttpError);
        }

        // adds or updates a resource
        function updateResource(resource) {
            return $http.post("api/UpdateResource", resource)
                .error(parseHttpError);
        }

        

        function updateResourceString(value, resourceId, resourceSet, localeId,comment) {
            var parm = {
                value: value,
                resourceId: resourceId,
                resourceSet: resourceSet,
                localeId: localeId,
                comment: comment
            };

            return $http.post("api/UpdateResourceString", parm)
                .error(parseHttpError);
        }

        function updateComment(comment, resourceId, resourceSet, localeId) {
            var parm = {                
                resourceId: resourceId,
                resourceSet: resourceSet,
                localeId: localeId,
                comment: comment
            };

            return $http.post("api/UpdateComment", parm)
                .error(parseHttpError);
        }

        function deleteResource(resourceId, resourceSet, localeId) {
            var parm = {                
                resourceId: resourceId,
                resourceSet: resourceSet,
                localeId: localeId
            };

            return $http.post("api/DeleteResource", parm)
                .error(parseHttpError);
        }

        function renameResource(resourceId, newResourceId, resourceSet) {
            var parm = {
                resourceId: resourceId,
                newResourceId: newResourceId,
                resourceSet: resourceSet                
            };

            return $http.post("api/RenameResource", parm)
                .error(parseHttpError);
        }

        function deleteResourceSet(resourceSet) {
           return $http.post("api/DeleteResourceSet?ResourceSet=" + resourceSet)
                .error(parseHttpError);
        }
        function renameResourceSet(oldResourceSet, newResourceSet) {
            return $http.post("api/RenameResourceSet?oldResourceSet=" + oldResourceSet + "&newResourceSet=" + newResourceSet)
                 .error(parseHttpError);
        }
        function reloadResources() {
            return $http.post("api/ReloadResources")
                 .error(parseHttpError);
        }
        function backup() {
            return $http.post("api/Backup")
                 .error(parseHttpError);
        }
        function createTable() {
            return $http.post("api/CreateTable")
                 .error(parseHttpError);
        }
        function createClass(file, namespace, resourceSets,classType) {
            return $http.post("api/CreateClass",
            { fileName: file, namespace: namespace, resourceSets: resourceSets, classType: classType || "DbRes" })
                    .error(parseHttpError);
        }
        function exportResxResources(path, resourceSets) {
            path = path || "";
            return $http.post("api/ExportResxResources",{  outputBasePath: path, resourceSets: resourceSets})
                    .error(parseHttpError);
        }
        function importResxResources(path) {
            path = path || "";
            return $http.post("api/ImportResxResources?inputBasePath=" + encodeURIComponent(path))
                    .error(parseHttpError);
        }
        function getLocalizationInfo() {
            // cache
            if (service.localizationInfo)
                return ww.angular.$httpPromiseFromValue($q,service.localizationInfo);
            
            return $http.get("api/GetLocalizationInfo")
                .success(function(info) {
                    service.localizationInfo = info;
                })
                .error(parseHttpError);            
        }
        function isRtl(localeId) {
            return $http.get("api/IsRtl?localeId=" + localeId)
                .error(parseHttpError);
        }
        function parseHttpError() {
            service.error = ww.angular.parseHttpError(arguments);
        }
    }
})();
    