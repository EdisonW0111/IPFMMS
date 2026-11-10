(() => {
    const form=document.getElementById('routineBillFilters');
    if(!form||!window.fetch||!window.AbortController)return;
    const error=document.getElementById('routineBillFilterError');let sequence=0,active,timer;
    function sync(url){const q=new URL(url,location.href).searchParams;form.elements.search.value=q.get('search')||'';form.elements.type.value=q.get('type')||'all';form.elements.status.value=q.get('status')||'all';}
    async function load(url,push=true){clearTimeout(timer);active?.abort();active=new AbortController();const request=++sequence,old=document.getElementById('scheduledBillResults');old.setAttribute('aria-busy','true');error.hidden=true;
        try{const response=await fetch(url,{signal:active.signal,credentials:'same-origin',cache:'no-store',headers:{'X-Requested-With':'XMLHttpRequest',Accept:'text/html'}});if(!response.ok)throw Error();const html=await response.text();if(request!==sequence)return;const next=new DOMParser().parseFromString(html,'text/html').getElementById('scheduledBillResults');if(!next){if(response.redirected){location.assign(response.url);return;}throw Error();}old.replaceWith(next);sync(url);if(push)history.pushState(null,'',url);}
        catch(e){if(e.name!=='AbortError'&&request===sequence)error.hidden=false;}
        finally{if(request===sequence)document.getElementById('scheduledBillResults').removeAttribute('aria-busy');}}
    function submit(){if(!form.reportValidity())return;const url=new URL(form.action,location.href);url.search=new URLSearchParams(new FormData(form)).toString();load(url.href);}
    form.addEventListener('submit',e=>{e.preventDefault();submit();});form.elements.search.addEventListener('input',()=>{clearTimeout(timer);++sequence;active?.abort();timer=setTimeout(submit,350);});for(const name of['type','status'])form.elements[name].addEventListener('change',submit);
    document.addEventListener('click',e=>{const a=e.target.closest('#scheduledBillResults nav a,#routineBillReset');if(!a||e.button!==0||e.ctrlKey||e.metaKey||e.shiftKey||e.altKey)return;e.preventDefault();load(a.href);});window.addEventListener('popstate',()=>load(location.href,false));document.getElementById('routineBillFilterButton').hidden=true;
})();
