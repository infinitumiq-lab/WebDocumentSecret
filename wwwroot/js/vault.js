document.addEventListener("DOMContentLoaded", () => {
    const typeSelect = document.getElementById("documentType");
    const formContainer = document.getElementById("documentFormContainer");
    const fieldsContainer = document.getElementById("documentFields");
    const form = document.getElementById("dynamicDocumentForm");
    const preview = document.getElementById("formPreviewMessage");
    const previewDetails = document.getElementById("previewDetails");
    const previewAttachments = document.getElementById("previewAttachments");
    const hidePreview = document.getElementById("hidePreview");
    if (!typeSelect || !formContainer || !fieldsContainer || !form) return;

    const apiSchemas = Array.isArray(window.allThatMattersFormSchemas) ? window.allThatMattersFormSchemas : [];
    const templates = [
      { id:"demo-insurance", name:"Insurance Policy", fields:["ProviderName","PolicyType","PolicyNumber","PolicyHolder","CoverageAmount","StartDate","RenewalDate","ClaimsContact","Notes"] },
      { id:"demo-life-insurance", name:"Life Insurance", fields:["ProviderName","PolicyNumber","PolicyHolder","Beneficiaries","CoverageAmount","PremiumAmount","StartDate","RenewalDate","ClaimsContact","Notes"] },
      { id:"demo-health-insurance", name:"Health Insurance", fields:["ProviderName","PolicyNumber","PolicyHolder","MemberNumber","CoverageDetails","StartDate","RenewalDate","ClaimsContact","Notes"] },
      { id:"demo-home-insurance", name:"Home / Property Insurance", fields:["ProviderName","PolicyNumber","InsuredProperty","PropertyAddress","CoverageAmount","StartDate","RenewalDate","ClaimsContact","Notes"] },
      { id:"demo-contract", name:"Contract / Agreement", fields:["ContractType","OtherParty","EffectiveDate","EndDate","RenewalTerms","NoticePeriod","StorageNotes"] },
      { id:"demo-passport", name:"Passport", fields:["FullName","PassportNumber","CountryOfIssue","IssueDate","ExpiryDate","IssuingAuthority","Notes"] },
      { id:"demo-identity", name:"Identity Document / Licence", fields:["DocumentKind","FullName","IdentificationNumber","IssuingAgency","IssueDate","ExpiryDate","Notes"] },
      { id:"demo-will", name:"Will / Estate Planning", fields:["DocumentTitle","LastUpdated","ExecutorName","ExecutorContact","StorageLocation","ReviewReminder","Notes"] },
      { id:"demo-property", name:"Property / Mortgage Document", fields:["PropertyAddress","DocumentType","InstitutionOrParty","ReferenceNumber","EffectiveDate","RenewalOrReviewDate","Notes"] },
      { id:"demo-education", name:"Education / Qualification", fields:["Institution","Qualification","CertificateNumber","IssueDate","ExpiryDate","Notes"] },
      { id:"demo-tax", name:"Tax Document", fields:["TaxYear","DocumentReference","FilingDate","DueDate","Notes"] },
      { id:"demo-bank", name:"Bank / Financial Document", fields:["InstitutionName","DocumentType","AccountReference","StatementPeriod","ReviewDate","Notes"] }
    ];
    const allTypes = [...apiSchemas];
    const seen = new Set(allTypes.map(x => x.documentTypeId));
    for (const template of templates) if (!seen.has(template.id)) allTypes.push({documentTypeId:template.id,typeName:template.name,fields:template.fields.map(keyName => ({keyName,inputType:inferType(keyName),options:optionsFor(keyName)})),isDemo:true});
    for (const item of allTypes) {
      if ([...typeSelect.options].some(o => o.value === item.documentTypeId)) continue;
      const option = document.createElement("option"); option.value = item.documentTypeId; option.textContent = item.typeName; typeSelect.appendChild(option);
    }

    typeSelect.addEventListener("change", () => {
      fieldsContainer.replaceChildren(); clearPreview();
      const schema = allTypes.find(item => item.documentTypeId === typeSelect.value);
      if (!schema) { formContainer.hidden = true; return; }
      for (const field of (schema.fields || [])) fieldsContainer.appendChild(createField(field));
      formContainer.hidden = false;
      const demoNote = document.createElement("p"); demoNote.className = "form-note";
      demoNote.textContent = schema.isDemo ? "Demonstration template: fields are illustrative and are not yet stored as an official form schema." : "Complete the details you want to keep. You can review them before leaving this page.";
      fieldsContainer.appendChild(demoNote);
    });

    form.addEventListener("submit", event => {
      event.preventDefault(); if (!form.reportValidity()) return;
      previewDetails.replaceChildren(); previewAttachments.replaceChildren();
      const entries = new FormData(form);
      for (const [key, value] of entries.entries()) {
        if (key === "__RequestVerificationToken" || key === "supportingFiles" || value instanceof File || !String(value).trim()) continue;
        const dt = document.createElement("dt"); dt.textContent = humanize(key);
        const dd = document.createElement("dd"); dd.textContent = String(value);
        previewDetails.append(dt, dd);
      }
      const files = document.getElementById("supportingFiles")?.files;
      if (files && files.length) {
        const heading = document.createElement("h3"); heading.textContent = "Selected supporting files"; previewAttachments.appendChild(heading);
        const list = document.createElement("ul");
        for (const file of files) { const li = document.createElement("li"); li.textContent = `${file.name} (${formatBytes(file.size)})`; list.appendChild(li); }
        previewAttachments.appendChild(list);
      }
      const warning = document.createElement("p"); warning.className = "preview-warning"; warning.textContent = "Preview only — these details and files have not been uploaded, saved, or encrypted.";
      previewAttachments.appendChild(warning);
      preview.hidden = false; preview.scrollIntoView({behavior:"smooth",block:"nearest"});
    });
    hidePreview?.addEventListener("click", clearPreview);
    form.addEventListener("reset", () => { window.setTimeout(clearPreview, 0); });
    function clearPreview(){ if(preview){preview.hidden=true;} if(previewDetails)previewDetails.replaceChildren(); if(previewAttachments)previewAttachments.replaceChildren(); }
    function createField(field) {
      const wrapper=document.createElement("div"); wrapper.className="form-group";
      const key=typeof field.keyName==="string"?field.keyName:"field";
      const label=document.createElement("label"); label.htmlFor=`field-${key}`; label.textContent=humanize(key); wrapper.appendChild(label);
      const inputType=(field.inputType||inferType(key)||"text").toLowerCase(); let control;
      if(inputType==="dropdown") { control=document.createElement("select"); control.className="form-control"; const opt=document.createElement("option"); opt.value=""; opt.textContent=`Select ${humanize(key)}`; control.appendChild(opt); for(const value of (Array.isArray(field.options)?field.options:optionsFor(key))){const option=document.createElement("option");option.value=String(value);option.textContent=String(value);control.appendChild(option);} }
      else if(inputType==="textarea"){control=document.createElement("textarea");control.rows=3;control.className="form-control";}
      else {control=document.createElement("input");control.className="form-control";const allowed=new Set(["text","password","date","number","email","tel","url"]);control.type=allowed.has(inputType)?inputType:"text";}
      control.id=`field-${key}`;control.name=key;control.autocomplete="off";control.required=false;wrapper.appendChild(control);return wrapper;
    }
    function inferType(key){const k=key.toLowerCase(); if(k.includes("date")||k.includes("expiry")||k.includes("renewal")||k.includes("effective")||k.includes("due"))return "date"; if(k.includes("email"))return "email"; if(k.includes("phone")||k.includes("contact"))return "text"; if(k.includes("amount")||k.includes("salary"))return "number"; if(k==="notes"||k.includes("details")||k.includes("terms")||k.includes("address")||k.includes("beneficiar"))return "textarea"; if(k.includes("type")||k.includes("kind")||k.includes("agency"))return "dropdown"; return "text";}
    function optionsFor(key){const k=key.toLowerCase(); if(k.includes("contracttype"))return ["Employment","Rental / Lease","Service Agreement","Sales / Purchase","Non-disclosure (NDA)","Partnership","Loan Agreement","Other"]; if(k.includes("documentkind"))return ["National ID Card","Driver's Licence","Voter's Card","Residence Permit","Other ID"]; if(k.includes("policytype"))return ["Life","Health","Motor / Auto","Home / Property","Travel","Business","Other"]; if(k.includes("documenttype"))return ["Agreement","Statement","Certificate","Correspondence","Other"]; if(k.includes("agency"))return ["Government Agency","Licensing Authority","Employer","School / Institution","Other"]; return [];}
    function humanize(value){return String(value||"Field").replace(/([a-z0-9])([A-Z])/g,"$1 $2").replace(/[_-]+/g," ").replace(/^./,c=>c.toUpperCase());}
    function formatBytes(bytes){if(bytes<1024)return `${bytes} B`;if(bytes<1048576)return `${(bytes/1024).toFixed(1)} KB`;return `${(bytes/1048576).toFixed(1)} MB`;}
});
